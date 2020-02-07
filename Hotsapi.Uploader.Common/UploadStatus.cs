using System;

namespace Hotsapi.Uploader.Common
{
    public interface IUploadStatus
    {
        bool IsSuccess { get; }
        bool IsRejected { get; }
    }
    public static class UploadStatus
    {
        public static IUploadStatus InProgress { get; } = new InProgress();
        public static IUploadStatus Successful(int id) => new UploadSuccess(id);
    }
    public readonly struct UploadSuccess : IUploadStatus
    {
        public bool IsRejected => false;
        public bool IsSuccess => true;
        public UploadSuccess(int id) => UploadID = id;
        public int UploadID { get; }
    }
    public readonly struct Rejected : IUploadStatus
    {
        public bool IsRejected => true;
        public bool IsSuccess => false;
        public RejectionReason Reason { get; }
        public string Details { get; }
        private Rejected(RejectionReason reason) : this(reason, reason.ToString()) { }
        private Rejected(RejectionReason reason, string details) => (Reason, Details) = (reason, details);
        public static IUploadStatus AiDetected { get; } = new Rejected(RejectionReason.AiDetected);
        public static IUploadStatus PtrRegion { get; } = new Rejected(RejectionReason.PtrRegion);
        public static IUploadStatus TooOld { get; } = new Rejected(RejectionReason.TooOld);
        public static IUploadStatus CustomGame { get; } = new Rejected(RejectionReason.CustomGame);
        public static IUploadStatus Duplicate { get; } = new Rejected(RejectionReason.Duplicate);
        public static IUploadStatus ForReason(RejectionReason reason)
        {
            switch (reason) {
                case RejectionReason.AiDetected: return AiDetected;
                case RejectionReason.PtrRegion: return PtrRegion;
                case RejectionReason.TooOld: return TooOld;
                case RejectionReason.CustomGame: return CustomGame;
                case RejectionReason.Duplicate: return Duplicate;
                default: return new Rejected(reason);
            }
        }

        public static IUploadStatus ForUnknownReason(string reason) => new Rejected(RejectionReason.UnknownReason, reason);
    }

    /// <summary>
    /// Represents a status that shouldn't occur, but allows the application
    /// to plod on regardless. Each occurrance of an instance of this class represents
    /// a bug in the uploader
    /// </summary>
    public readonly struct InternalError : IUploadStatus {
        public InternalError(string description) => ErrorDescription = description;
        public bool IsSuccess => false;
        public bool IsRejected => false;
        public string ErrorDescription { get; }
    }
    //for matching
    public interface IFailed : IUploadStatus {
        public Exception RawException { get; }
    }
    public readonly struct Failed<E> : IFailed where E : Exception {
        public Failed(E cause) => Cause = cause;
        public E Cause { get; }
        public Exception RawException => Cause;
        public bool IsSuccess => false;
        public bool IsRejected => false;
    }

    public readonly struct InProgress : IUploadStatus {
        public bool IsSuccess => false;
        public bool IsRejected => false;
    }

    public enum RejectionReason
    {
        Duplicate,
        AiDetected,
        CustomGame,
        PtrRegion,
        Incomplete,
        TooOld,
        UnknownReason
    }
}
