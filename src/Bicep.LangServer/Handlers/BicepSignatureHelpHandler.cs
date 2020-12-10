// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;
using Bicep.LanguageServer.CompilationManager;
using Bicep.LanguageServer.Completions;
using Bicep.LanguageServer.Utils;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SymbolKind = Bicep.Core.Semantics.SymbolKind;

namespace Bicep.LanguageServer.Handlers
{
    public class BicepSignatureHelpHandler: SignatureHelpHandler
    {
        private readonly ICompilationManager compilationManager;

        public BicepSignatureHelpHandler(ICompilationManager compilationManager) : base(CreateRegistrationOptions())
        {
            this.compilationManager = compilationManager;
        }

        public override Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
        {
            // local function
            static Task<SignatureHelp?> NoHelp() => Task.FromResult<SignatureHelp?>(null);

            CompilationContext? context = this.compilationManager.GetCompilation(request.TextDocument.Uri);
            if (context == null)
            {
                return NoHelp();
            }

            int offset = PositionHelper.GetOffset(context.LineStarts, request.Position);
            var matchingNodes = SyntaxMatcher.FindNodesMatchingOffset(context.ProgramSyntax, offset);

            var functionCall = SyntaxMatcher.FindLastNodeOfType<FunctionCallSyntax, FunctionCallSyntax>(matchingNodes).node;
            if (functionCall == null)
            {
                return NoHelp();
            }

            var symbol = context.Compilation.GetEntrypointSemanticModel().GetSymbolInfo(functionCall);
            if (symbol is not FunctionSymbol functionSymbol)
            {
                // no symbol or symbol is not a function
                return NoHelp();
            }

            return Task.FromResult<SignatureHelp?>(new SignatureHelp
            {
                Signatures = new Container<SignatureInformation>(functionSymbol.Overloads.Select(overload => new SignatureInformation
                {
                    Label = overload.TypeSignature,
                    Documentation = new StringOrMarkupContent(new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = $"The `{symbol.Name}` function."
                    })
                }))
            });
        }

        private static SignatureHelpRegistrationOptions CreateRegistrationOptions() => new SignatureHelpRegistrationOptions
        {
            DocumentSelector = DocumentSelectorFactory.Create(),
            TriggerCharacters = new Container<string>("("),
            RetriggerCharacters = new Container<string>(",")
        };
    }
}
