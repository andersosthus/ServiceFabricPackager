﻿using System;
using SFPackager.Models;
using SFPackager.Models.Xml;
using SFPackager.Services.Manifest;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace SFPackager.Services
{
    public class SfProjectHandler
    {
        private readonly ManifestParser _appManifestHandler;
        private readonly AppConfig _baseConfig;
        private readonly PackageConfig _packageConfig;

        public SfProjectHandler(ManifestParser appManifestHandler, AppConfig baseConfig, PackageConfig packageConfig)
        {
            _appManifestHandler = appManifestHandler;
            _baseConfig = baseConfig;
            _packageConfig = packageConfig;
        }

        public ServiceFabricApplicationProject Parse(
            ServiceFabricApplicationProject sfProject,
            DirectoryInfo srcBasePath)
        {
            var basePath = Path.GetDirectoryName(sfProject.ProjectFileFullPath);

            using (var fileStream = new FileStream(sfProject.ProjectFileFullPath, FileMode.Open))
            using (var reader = XmlReader.Create(fileStream))
            {
                var document = new XmlDocument();
                document.Load(reader);
                var manager = new XmlNamespaceManager(document.NameTable);
                manager.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

                sfProject.ApplicationManifestPath = ExtractApplicationManifest(basePath, document, manager);
                sfProject = _appManifestHandler.ReadXml(sfProject);

                sfProject.Services = ExtractProjectReferences(basePath, sfProject.BuildOutputPathSuffix, document, manager);

                var guestExecutables = DiscoverAndReadGuestExecutables(sfProject);
                foreach (var guest in guestExecutables)
                {
                    if(!sfProject.Services.ContainsKey(guest.Key))
                        sfProject.Services.Add(guest.Key, guest.Value);
                }

                return sfProject;
            }
        }

        private Dictionary<string, ServiceFabricServiceProject> DiscoverAndReadGuestExecutables(ServiceFabricApplicationProject sfProject)
        {
            var result = new Dictionary<string, ServiceFabricServiceProject>();

            var guests = _packageConfig.GuestExecutables.Where(x => x.ApplicationTypeName.Equals(sfProject.ApplicationTypeName, StringComparison.CurrentCultureIgnoreCase));

            foreach (var guest in guests)
            {
                var serviceProject = new ServiceFabricServiceProject
                {
                    IsAspNetCore = false,
                    ProjectFolder = new DirectoryInfo(Path.Combine(_baseConfig.SourcePath.FullName, guest.PackageName)),
                    ProjectFile = null,
                    PackageRoot = new DirectoryInfo(Path.Combine(_baseConfig.SourcePath.FullName, guest.PackageName)),
                };

                var finalProject = _appManifestHandler.ReadXml(serviceProject, Path.Combine(serviceProject.ProjectFolder.FullName, "Code"));
                finalProject.IsGuestExecutable = true;

                result.Add(guest.PackageName, finalProject);
            }

            return result;
        }

        internal static string ExtractApplicationManifest(
            string basePath,
            XmlNode document,
            XmlNamespaceManager namespaceManager)
        {
            var contents = document.SelectSingleNode("//*[@Include='ApplicationPackageRoot\\ApplicationManifest.xml']/@Include", namespaceManager);

            if (!(contents is XmlAttribute))
                return string.Empty;

            var attr = contents as XmlAttribute;
            var path = Path.Combine(basePath, attr.Value);

            return Path.GetDirectoryName(path);
        }

        private Dictionary<string, ServiceFabricServiceProject> ExtractProjectReferences(
            string basePath,
            string buildOutputPathSuffix,
            XmlNode document,
            XmlNamespaceManager namespaceManager)
        {
            var projectReferences = new Dictionary<string, ServiceFabricServiceProject>();

            var projects = document.SelectNodes("//x:ProjectReference/@Include", namespaceManager);

            foreach (var service in projects)
            {
                if (!(service is XmlAttribute))
                    continue;

                var attr = service as XmlAttribute;
                var projectFile = new FileInfo(Path.Combine(basePath, attr.Value));

                var serviceProject = new ServiceFabricServiceProject
                {
                    ProjectFolder = projectFile.Directory,
                    ProjectFile = projectFile
                };

                // TODO Ugly Asp.Net hack thing.

                ServiceFabricServiceProject projectInfo;
                string buildOutputPath;

                var projectFileContents = File.ReadAllText(projectFile.FullName, System.Text.Encoding.UTF8);
                if (projectFileContents.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">"))
                {
                    var loader = new ManifestLoader<CoreProjectFile>(false);
                    var projectModel = loader.Load(projectFile.FullName);

                    var propertyGroup = projectModel.PropertyGroup[0];

                    buildOutputPath = Path.Combine(serviceProject.ProjectFolder.FullName, "bin", _baseConfig.BuildConfiguration, propertyGroup.TargetFramework, propertyGroup.RuntimeIdentifiers[0]);
                    projectInfo = _appManifestHandler.ReadXml(serviceProject, buildOutputPath);
                    projectInfo.IsAspNetCore = true;
                }
                else
                {
                    buildOutputPath = Path.Combine(serviceProject.ProjectFolder.FullName, buildOutputPathSuffix);
                    projectInfo = _appManifestHandler.ReadXml(serviceProject, buildOutputPath);
                    projectInfo.IsAspNetCore = false;
                }

                projectReferences.Add(projectInfo.ServiceName, projectInfo);
            }

            return projectReferences;
        }
    }
}