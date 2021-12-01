// This file is part of Core WF which is licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System.Activities.Statements;

namespace System.Activities.Expressions;

#if DYNAMICUPDATE
using System.Activities.DynamicUpdate;
#endif

//[SuppressMessage(FxCop.Category.Naming, FxCop.Rule.IdentifiersShouldNotMatchKeywords, Justification = "Optimizing for XAML naming. VB imperative users will [] qualify (e.g. New [OrElse])")]
public sealed class OrElse : Activity<bool>
{
    public OrElse()
        : base()
    {
        Implementation =
            () =>
            {
                if (Left != null && Right != null)
                {
                    return new If
                    {
                        Condition = Left,
                        Then = new Assign<bool>
                        {
                            To = new OutArgument<bool>(context => Result.Get(context)),
                            Value = true,
                        },
                        Else = new Assign<bool>
                        {
                            To = new OutArgument<bool>(context => Result.Get(context)),
                            Value = new InArgument<bool>(Right)
                        }
                    };
                }
                else
                {
                    return null;
                }
            };
    }

    [DefaultValue(null)]
    public Activity<bool> Left { get; set; }

    [DefaultValue(null)]
    public Activity<bool> Right { get; set; }

#if DYNAMICUPDATE
    protected override void OnCreateDynamicUpdateMap(UpdateMapMetadata metadata, Activity originalActivity)
    {
        metadata.AllowUpdateInsideThisActivity();
    } 
#endif

    protected override void CacheMetadata(ActivityMetadata metadata)
    {
        metadata.AddImportedChild(Left);
        metadata.AddImportedChild(Right);

        if (Left == null)
        {
            metadata.AddValidationError(SR.BinaryExpressionActivityRequiresArgument("Left", "OrElse", DisplayName));
        }

        if (Right == null)
        {
            metadata.AddValidationError(SR.BinaryExpressionActivityRequiresArgument("Right", "OrElse", DisplayName));
        }
    }
}
