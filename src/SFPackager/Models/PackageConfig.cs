﻿using System.Collections.Generic;

namespace SFPackager.Models
{
    public class PackageConfig
    {
        public List<HttpsConfig> Https { get; set; }
        public List<ExternalInclude> ExternalIncludes { get; set; }
        public ClusterConfig Cluster { get; set; }
    }

    public class ExternalInclude
    {
        public string ApplicationTypeName { get; set; }
        public string ServiceManifestName { get; set; }
        public string PackageName { get; set; }
        public string SourceFileName { get; set; }
        public string TargetFileName { get; set; }
        public string Source { get; set; }
    }

    public class HttpsConfig
    {
        public string ApplicationTypeName { get; set; }
        public string ServiceManifestName { get; set; }
        public string EndpointName { get; set; }
        public string CertThumbprint { get; set; }
    }

    public class ClusterConfig
    {
        public string Endpoint { get; set; }
        public int Port { get; set; }
        public string PfxFile { get; set; }
        public string PfxKey { get; set; }
    }
}