﻿using Rubberduck.Parsing.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Rubberduck.Refactorings.CreateUDTMember
{
    public class CreateUDTMemberModel : IRefactoringModel
    {
        private Dictionary<Declaration, List<(Declaration prototype, string UDTMemberIdentifier)>> _targets { get; } = new Dictionary<Declaration, List<(Declaration, string)>>();

        public CreateUDTMemberModel()
        { }

        public CreateUDTMemberModel(Declaration userDefinedType, IEnumerable<(Declaration prototype, string UserDefinedTypeMemberIdentifier)> conversionModels)
        {
            if (conversionModels.Any(cm => !IsValidPrototypeDeclarationType(cm.prototype.DeclarationType)))
            {
                throw new ArgumentException();
            }

            foreach ((Declaration prototype, string UDTMemberIdentifier) in conversionModels)
            {
                AssignPrototypeToUserDefinedType(userDefinedType, prototype, UDTMemberIdentifier);
            }
        }

        public IReadOnlyCollection<Declaration> UserDefinedTypeTargets => _targets.Keys;

        public IEnumerable<(Declaration prototype, string userDefinedTypeMemberIdentifier)> this[Declaration udt] 
            => _targets[udt].Select(pr => (pr.prototype, pr.UDTMemberIdentifier));

        private void AssignPrototypeToUserDefinedType(Declaration udt, Declaration prototype, string udtMemberIdentifierName = null)
        {
            if (!udt.DeclarationType.HasFlag(DeclarationType.UserDefinedType))
            {
                throw new ArgumentException();
            }

            if (!(_targets.TryGetValue(udt, out var memberPrototypes)))
            {
                _targets.Add(udt, new List<(Declaration, string)>());
            }
            else
            {
                var hasDuplicateMemberNames = memberPrototypes
                    .Select(pr => pr.UDTMemberIdentifier?.ToUpperInvariant() ?? pr.prototype.IdentifierName)
                    .GroupBy(uc => uc).Any(g => g.Count() > 1);

                if (hasDuplicateMemberNames)
                {
                    throw new ArgumentException();
                }
            }

            _targets[udt].Add((prototype, udtMemberIdentifierName ?? prototype.IdentifierName));
        }

        private static bool IsValidPrototypeDeclarationType(DeclarationType declarationType)
        {
            return declarationType.HasFlag(DeclarationType.Variable)
                || declarationType.HasFlag(DeclarationType.UserDefinedTypeMember)
                || declarationType.HasFlag(DeclarationType.Constant)
                || declarationType.HasFlag(DeclarationType.Function);
        }
    }
}
