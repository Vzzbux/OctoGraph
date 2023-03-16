using System.ComponentModel.DataAnnotations;

namespace OctoGraph.Models
{
    public class OctoGraphOptions
    {
        public const string ConfigSection = "OctoGraph";

        [Required]
        public OctopusServer[] OctopusInstances { get; set; }
        public double CacheTimeoutInMinutes { get; set; }
        public bool AuthenticationEnabled { get; set; }
    }
    public struct OctopusServer
    {
        public string Label { get; set; }
        public string Hostname { get; set; }
        public string ApiKey { get; set; }
    }
}
