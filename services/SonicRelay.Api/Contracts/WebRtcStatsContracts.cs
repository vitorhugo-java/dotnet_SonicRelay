namespace SonicRelay.Api.Contracts;

/// <summary>
/// A periodic WebRTC stats snapshot reported by a client (issue #21). Mirrors the
/// subset of the browser/native <c>getStats()</c> output needed to diagnose packet
/// loss, jitter, RTT and the selected transport, without carrying SDP or full ICE
/// candidates. All nested objects are optional so clients can send what they have.
/// </summary>
public sealed record WebRtcStatsReport(
    Guid SessionId,
    string Role,
    string? IceConnectionState,
    bool IceRestart,
    SelectedCandidatePairReport? SelectedCandidatePair,
    InboundAudioReport? InboundAudio,
    CandidatePairReport? CandidatePair);

public sealed record SelectedCandidatePairReport(
    string? LocalCandidateType,
    string? RemoteCandidateType,
    string? Protocol,
    string? RelayProtocol);

public sealed record InboundAudioReport(
    long? PacketsReceived,
    long? PacketsLost,
    double? Jitter,
    long? ConcealedSamples,
    long? TotalSamplesReceived);

public sealed record CandidatePairReport(
    double? CurrentRoundTripTime,
    double? AvailableIncomingBitrate,
    double? AvailableOutgoingBitrate);
