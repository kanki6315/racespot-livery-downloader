using System;
using System.Runtime.Serialization;

namespace WpfApp1
{
    public class Livery
    {
        public string ITeamId { get; set; }
        public string ITeamName { get; set; }
        public bool IsRejected { get; set; }
        public bool IsCustomNumber { get; set; }
        public string IracingId { get; set; }
        public LiveryType LiveryType { get; set; }
        public RejectionStatus RejectionStatus { get; set; }
        public string carPath { get; set; }

        public bool IsTeam()
        {
            return !String.IsNullOrEmpty(ITeamId);
        }
    }

    public enum LiveryType
    {
        Car,
        Suit,
        Helmet,
        [EnumMember(Value = "Spec Map")]
        SpecMap
    }

    public enum RejectionStatus
    {
        Rejected, Updated, Resolved
    }
}