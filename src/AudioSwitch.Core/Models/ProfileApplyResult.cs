namespace AudioSwitch.Core.Models;

public sealed record ProfileApplyResult(
    AudioProfile Profile,
    IReadOnlyList<ProfileApplyStepError> Errors)
{
    public bool IsFullSuccess => Errors.Count == 0;
}

public sealed record ProfileApplyStepError(
    string Step,
    string DeviceId,
    string Message);
