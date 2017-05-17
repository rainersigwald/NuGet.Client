﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.VisualStudio;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// A nuget aware project system containing a .json file instead of a packages.config file
    /// </summary>
    public class ProjectJsonBuildIntegratedProjectSystem : ProjectJsonBuildIntegratedNuGetProject
    {
        private readonly EnvDTEProject _envDTEProject;
        private IScriptExecutor _scriptExecutor;

        public ProjectJsonBuildIntegratedProjectSystem(
            string jsonConfigPath,
            string msbuildProjectFilePath,
            EnvDTEProject envDTEProject,
            string uniqueName)
            : base(jsonConfigPath, msbuildProjectFilePath)
        {
            _envDTEProject = envDTEProject;

            // set project id
            var projectId = VsHierarchyUtility.GetProjectId(envDTEProject);
            InternalMetadata.Add(NuGetProjectMetadataKeys.ProjectId, projectId);

            UpdateInternalTargetFramework();

            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, uniqueName);
        }

        private void UpdateInternalTargetFramework()
        {
            // Override the JSON TFM value from the csproj for UAP framework
            if (InternalMetadata.TryGetValue(NuGetProjectMetadataKeys.TargetFramework, out object targetFramework))
            {
                var jsonTargetFramework = targetFramework as NuGetFramework;
                if (IsUAPFramework(jsonTargetFramework))
                {
                    // This needs to be on UI Thread.
                    var platfromMinVersion = VsHierarchyUtility.GetMSBuildProperty(VsHierarchyUtility.ToVsHierarchy(_envDTEProject), EnvDTEProjectInfoUtility.TargetPlatformMinVersion);

                    if (!string.IsNullOrEmpty(platfromMinVersion))
                    {
                        // Found the TPMinV in csproj, store this as a new target framework to be replaced in project.json
                        var newTargetFramework = new NuGetFramework(jsonTargetFramework.Framework, new Version(platfromMinVersion));
                        InternalMetadata[NuGetProjectMetadataKeys.TargetFramework] = newTargetFramework;
                    }
                }
            }
        }

        private IScriptExecutor ScriptExecutor
        {
            get
            {
                if (_scriptExecutor == null)
                {
                    _scriptExecutor = ServiceLocator.GetInstanceSafe<IScriptExecutor>();
                }

                return _scriptExecutor;
            }
        }

        public override async Task<bool> ExecuteInitScriptAsync(
            PackageIdentity identity,
            string packageInstallPath,
            INuGetProjectContext projectContext,
            bool throwOnFailure)
        {
            return
                await
                    ScriptExecutorUtil.ExecuteScriptAsync(identity, packageInstallPath, projectContext, ScriptExecutor,
                        _envDTEProject, throwOnFailure);
        }

        public override Task<IReadOnlyList<ProjectRestoreReference>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var resolvedProjects = context.DeferredPackageSpecs.Select(project => project.Name);

            UpdateInternalTargetFramework();

            return VSProjectRestoreReferenceUtility.GetDirectProjectReferences(_envDTEProject, resolvedProjects, context.Logger);
        }
    }
}
