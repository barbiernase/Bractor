using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Feedback;

namespace Domain.Client.Modules.Feedback;
public class FeedbackModule : IHeadlessModule
{
    public string Id    => "feedback";
    public string Title => "Feedback";
    public Type   ComponentType => typeof(FeedbackEffects);
}
