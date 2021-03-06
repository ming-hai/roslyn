﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Diagnostics.EngineV2;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Shared.Options;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    [ExportIncrementalAnalyzerProvider(
        highPriorityForActiveFile: true, name: WellKnownSolutionCrawlerAnalyzers.Diagnostic,
        workspaceKinds: new string[] { WorkspaceKind.Host, WorkspaceKind.Interactive, WorkspaceKind.AnyCodeRoslynWorkspace })]
    internal partial class DiagnosticAnalyzerService : IIncrementalAnalyzerProvider
    {
        private readonly ConditionalWeakTable<Workspace, DiagnosticIncrementalAnalyzer> _map;
        private readonly ConditionalWeakTable<Workspace, DiagnosticIncrementalAnalyzer>.CreateValueCallback _createIncrementalAnalyzer;

        [SuppressMessage("RoslyDiagnosticsReliability", "RS0034:Exported parts should have [ImportingConstructor]", Justification = "Private constructor used for deterministic field initialization")]
        private DiagnosticAnalyzerService()
        {
            _map = new ConditionalWeakTable<Workspace, DiagnosticIncrementalAnalyzer>();
            _createIncrementalAnalyzer = CreateIncrementalAnalyzerCallback;
        }

        public IIncrementalAnalyzer CreateIncrementalAnalyzer(Workspace workspace)
        {
            if (!workspace.Options.GetOption(ServiceComponentOnOffOptions.DiagnosticProvider))
            {
                return null;
            }

            return _map.GetValue(workspace, _createIncrementalAnalyzer);
        }

        public void ShutdownAnalyzerFrom(Workspace workspace)
        {
            // this should be only called once analyzer associated with the workspace is done.
            if (_map.TryGetValue(workspace, out var analyzer))
            {
                analyzer.Shutdown();
            }
        }

        private DiagnosticIncrementalAnalyzer CreateIncrementalAnalyzerCallback(Workspace workspace)
        {
            // subscribe to active context changed event for new workspace
            workspace.DocumentActiveContextChanged += OnDocumentActiveContextChanged;

            return new DiagnosticIncrementalAnalyzer(this, LogAggregator.GetNextId(), workspace, HostAnalyzers, AnalyzerInfoCache, _hostDiagnosticUpdateSource);
        }

        private void OnDocumentActiveContextChanged(object sender, DocumentActiveContextChangedEventArgs e)
        {
            Reanalyze(e.Solution.Workspace, documentIds: SpecializedCollections.SingletonEnumerable(e.NewActiveContextDocumentId), highPriority: true);
        }
    }
}
