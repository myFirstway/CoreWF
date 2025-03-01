﻿using WorkflowApplicationTestExtensions.Persistence;
using System;
using System.Activities;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using StringToObject = System.Collections.Generic.IDictionary<string, object>;
using System.Collections;
using System.Activities.Hosting;
using System.Collections.Generic;

namespace WorkflowApplicationTestExtensions;

public static class WorkflowApplicationTestExtensions
{
    public const string AutoResumedBookmarkNamePrefix = "AutoResumedBookmark_";

    public record WorkflowApplicationResult(StringToObject Outputs, int PersistenceCount, IEnumerable<BookmarkInfo> PersistIdle, IEnumerable<BookmarkInfo> UnloadedBookmarks);

    /// <summary>
    /// Simple API to wait for the workflow to complete or propagate to the caller any error.
    /// Also, when PersistableIdle, will automatically Unload, Load, resume some bookmarks
    /// (those named "AutoResumedBookmark_...") and continue execution.
    /// </summary>
    public static WorkflowApplicationResult RunUntilCompletion(this WorkflowApplication application,
        Action<WorkflowApplication> beforeResume = null, bool useJsonSerialization = false)
    {
        var applicationId = application.Id;
        var persistenceCount = 0;
        var output = new TaskCompletionSource<WorkflowApplicationResult>();
        IEnumerable<BookmarkInfo> persistentBookmarks = null;
        IEnumerable<BookmarkInfo> unloadedBookmarks = null;
        application.Completed += (WorkflowApplicationCompletedEventArgs args) =>
        {
            if (args.TerminationException is { } ex)
            {
                output.TrySetException(ex);
            }
            if (args.CompletionState == ActivityInstanceState.Canceled)
            {
                throw new OperationCanceledException("Workflow canceled.");
            }
            output.TrySetResult(new(args.Outputs, persistenceCount, persistentBookmarks, unloadedBookmarks));
            application = null;
        };

        application.Aborted += args =>
        {
            output.TrySetException(args.Reason);
        };

        application.InstanceStore ??= new MemoryInstanceStore(
            useJsonSerialization
            ? new JsonWorkflowSerializer()
            : new DataContractWorkflowSerializer()
        );

        application.Unloaded += uargs =>
        {
            Debug.WriteLine("Unloaded");
            if (application == null)
                return;
            application.Load(applicationId);
            unloadedBookmarks = application.GetBookmarks();
            foreach (var bookmark in application.GetBookmarks().Where(b => b.BookmarkName.StartsWith(AutoResumedBookmarkNamePrefix)))
            {
                application.ResumeBookmark(new Bookmark(bookmark.BookmarkName), null);
            }
        };
        application.PersistableIdle += (WorkflowApplicationIdleEventArgs args) =>
        {
            Debug.WriteLine("PersistableIdle");
            try
            {
                persistentBookmarks = args.Bookmarks;
                if (++persistenceCount > 1000)
                {
                    throw new Exception("Persisting too many times, aborting test.");
                }
                application = CloneWorkflowApplication(application);
                beforeResume?.Invoke(application);
            }
            catch (Exception ex)
            {
                output.TrySetException(ex);
            }
            return PersistableIdleAction.Unload;
        };

        application.Run();

        try
        {
            output.Task.Wait(TimeSpan.FromSeconds(15));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
        return output.Task.GetAwaiter().GetResult();
    }

    private static WorkflowApplication CloneWorkflowApplication(WorkflowApplication application)
    => new(application.WorkflowDefinition, application.DefinitionIdentity)
        {
            Aborted = application.Aborted,
            Completed = application.Completed,
            PersistableIdle = application.PersistableIdle,
            Unloaded = application.Unloaded,
            InstanceStore = application.InstanceStore,
        };
}
