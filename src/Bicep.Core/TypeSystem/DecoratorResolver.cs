﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Collections.Immutable;
using Bicep.Core.Semantics;
using Bicep.Core.Syntax;

namespace Bicep.Core.TypeSystem
{
    public class DecoratorResolver
    {
        private readonly ImmutableDictionary<FunctionOverload, Decorator> decoratorsByOverloads;

        private readonly FunctionResolver functionResolver;

        private readonly ImmutableHashSet<FunctionSymbol> functionSymbols;

        public DecoratorResolver(IEnumerable<Decorator> decorators)
        {
            this.decoratorsByOverloads = decorators.ToImmutableDictionary(decorator => decorator.Overload, decorator => decorator);
            this.functionResolver = FunctionResolver.Create(decoratorsByOverloads.Keys);
            this.functionSymbols = functionResolver.GetKnownFunctions().Values.ToImmutableHashSet();
        }

        public Symbol? TryGetSymbol(IdentifierSyntax identifierSyntax) => this.functionResolver.TryGetSymbol(identifierSyntax);

        public ImmutableDictionary<string, FunctionSymbol> GetKnownDecoratorFunctions() => this.functionResolver.GetKnownFunctions();

        public IEnumerable<Decorator> GetMatches(FunctionSymbol symbol, IList<TypeSymbol> argumentTypes)
        {
            if (!functionSymbols.Contains(symbol))
            {
                yield break;
            }

            foreach (var overload in FunctionResolver.GetMatches(symbol, argumentTypes, out var _, out var _))
            {
                this.decoratorsByOverloads.TryGetValue(overload, out Decorator? decorator);

                if (decorator != null)
                {
                    yield return decorator;
                }
            }
        }
    }
}