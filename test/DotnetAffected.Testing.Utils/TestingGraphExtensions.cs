﻿using Microsoft.Build.Construction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotnetAffected.Testing.Utils
{
    public static class TestingGraphExtensions
    {
        public static ProjectRootElement SetName(this ProjectRootElement element, string name)
        {
            element.AddProperty("ProjectName", name);
            return element;
        }

        public static string GetName(this ProjectRootElement element)
        {
            var fi = new FileInfo(element.FullPath);
            return fi.Name[..^fi.Extension.Length];
        }
        
        public static ProjectRootElement AddProjectDependency(this ProjectRootElement element, string dependencyPath)
        {
            element.AddItem("ProjectReference", dependencyPath);
            return element;
        }

        public static ProjectRootElement AddNuGetDependency(this ProjectRootElement element, string packageName,
            string version = null)
        {
            var metaData = version != null
                ? new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("Version", version)
                }
                : null;

            element.AddItem("PackageReference", packageName, metaData);
            return element;
        }

        public static ProjectRootElement CreateDirectoryPackageProps(this ProjectRootElement element, bool importParent, Action<ProjectRootElement> customizer)
        {
            
            var fi = new FileInfo(element.FullPath);
            var path = Path.Combine(fi.DirectoryName ?? "", "Directory.Packages.props");
            var project = ProjectRootElement.Create(path);

            if (importParent)
                project.AddImport("$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory).., Directory.Packages.props))" + Path.DirectorySeparatorChar + "Directory.Packages.props");

            customizer?.Invoke(project);

            project.Save();

            return project;
        }

        public static ProjectRootElement UpdateDirectoryPackageProps(this ProjectRootElement element, Action<ProjectRootElement> customizer)
        {
            var fi = new FileInfo(element.FullPath);
            var path = Path.Combine(fi.DirectoryName ?? "", "Directory.Packages.props");
            var project = ProjectRootElement.Open(path) ??
                          throw new InvalidOperationException("Failed to load msbuild project");

            customizer?.Invoke(project);

            project.Save();

            return project;
        }
        
        public static void AddOrUpdateProperty(this ProjectRootElement element, string propertyName, string propertyValue)
        {
            var matches = element.Properties.Where(p => p.Name == propertyName).ToList();
            if (matches.Any())
            {
                foreach (var pe in matches)
                    pe.Value = propertyValue;    
            }
            else
            {
                element.AddProperty(propertyName, propertyValue);
            }
            
        }
        

        public static void RemoveDirectoryPackageProps(this ProjectRootElement element)
        {
            element.DeleteFile("Directory.Packages.props");
        }

        public static void DeleteFile(this ProjectRootElement element, string relativePath)
        {
            var fi = new FileInfo(element.FullPath);
            var path = Path.Combine(fi.DirectoryName ?? "", relativePath);
            File.Delete(path);
        }

        public static ProjectRootElement OptOutFromCentrallyManagedNuGetPackageVersions(this ProjectRootElement element)
        {
            element.AddProperty("ManagePackageVersionsCentrally", "false");
            return element;
        }

        public static ProjectRootElement AddPackageVersion(this ProjectRootElement element, string packageName, string version)
        {
            var itemGroup = element.ItemGroups.FirstOrDefault() ?? element.AddItemGroup();

            var item = itemGroup.AddItem("PackageVersion", packageName);
            item.AddMetadata("Version", version, expressAsAttribute: true);

            return element;
        }

        public static ProjectRootElement UpdatePackageVersion(
            this ProjectRootElement element,
            string packageName,
            string newVersion)
        {
            Exception BuildException(string message)
            {
                return new InvalidOperationException(
                    "Failed to understand Directory.Package.props\n" + message);
            }

            var itemGroup = element.ItemGroups.FirstOrDefault()
                            ?? throw BuildException("Could not find a first level ItemGroup to update");

            var toUpdate = itemGroup.Children
                               .Where(c => c is ProjectItemElement)
                               .Cast<ProjectItemElement>()
                               .SingleOrDefault(p => p.ElementName == "PackageVersion" && p.Include == packageName)
                           ?? throw BuildException($"Could not find a PackageVersion element for {packageName}");

            if (newVersion is null)
            {
                itemGroup.RemoveChild(toUpdate);
            }
            else
            {
                var versionToUpdate = toUpdate.Metadata
                                          .SingleOrDefault(m => m.Name == "Version")
                                      ?? throw BuildException(
                                          $"Could not find a Version metadata in ItemGroup {toUpdate}");
                versionToUpdate.Value = newVersion;
            }

            return element;
        }
    }
}
