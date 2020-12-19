// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Deployments.Core.Extensions;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;
using Bicep.Core.TypeSystem;
using Bicep.LanguageServer.CompilationManager;
using Bicep.LanguageServer.Completions;
using Bicep.LanguageServer.Utils;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

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

            var semanticModel = context.Compilation.GetEntrypointSemanticModel();
            var symbol = semanticModel.GetSymbolInfo(functionCall);
            if (symbol is not FunctionSymbol functionSymbol)
            {
                // no symbol or symbol is not a function
                return NoHelp();
            }

            var argumentTypes = functionCall.Arguments.Select(arg => semanticModel.GetTypeInfo(arg)).ToList();

            return Task.FromResult<SignatureHelp?>(CreateSignatureHelp(argumentTypes, functionSymbol));
        }

        private SignatureHelp CreateSignatureHelp(List<TypeSymbol> argumentTypes, FunctionSymbol symbol)
        {
            var matchingOverloads = symbol.Overloads
                .Where(fo => !fo.MaximumArgumentCount.HasValue || argumentTypes.Count <= fo.MaximumArgumentCount.Value)
                .Select(overload => (overload, result: overload.Match(argumentTypes, out _, out _)))
                .ToList();

            int activeSignatureIndex = matchingOverloads.IndexOf(tuple => tuple.result == FunctionMatchResult.Match);
            if (activeSignatureIndex < 0)
            {
                // no best match - try potential match
                activeSignatureIndex = matchingOverloads.IndexOf(tuple => tuple.result == FunctionMatchResult.PotentialMatch);
            }

            return new SignatureHelp
            {
                Signatures = new Container<SignatureInformation>(matchingOverloads.Select(tuple => CreateSignature(tuple.overload))),
                ActiveSignature = activeSignatureIndex < 0 ? (int?) null : activeSignatureIndex
            };
        }

        private SignatureInformation CreateSignature(FunctionOverload overload)
        {
            const string delimiter = ", ";

            var typeSignature = new StringBuilder();
            var parameters = new List<ParameterInformation>();

            typeSignature.Append(overload.Name);
            typeSignature.Append('(');

            foreach (var fixedParameter in overload.FixedParameters)
            {
                AppendParameter(typeSignature, parameters, fixedParameter.Signature, fixedParameter.Description);
                typeSignature.Append(delimiter);
            }

            // TODO: Adjust based specified parameters
            if (overload.VariableParameter != null)
            {
                AppendParameter(typeSignature, parameters, overload.VariableParameter.Signature, overload.VariableParameter.Description);
                typeSignature.Append(delimiter);
            }

            if (parameters.Any())
            {
                typeSignature.Remove(typeSignature.Length - delimiter.Length, delimiter.Length);
            }

            typeSignature.Append(')');

            return new SignatureInformation
            {
                Label = typeSignature.ToString(),
                Documentation = overload.Description,
                Parameters = new Container<ParameterInformation>(parameters)
            };
        }

        private static void AppendParameter(StringBuilder typeSignature, List<ParameterInformation> parameterInfos, string parameterSignature, string documentation)
        {
            int start = typeSignature.Length;
            typeSignature.Append(parameterSignature);
            int end = typeSignature.Length;

            parameterInfos.Add(new ParameterInformation
            {
                Label = new ParameterInformationLabel((start, end)),
                Documentation = documentation
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
