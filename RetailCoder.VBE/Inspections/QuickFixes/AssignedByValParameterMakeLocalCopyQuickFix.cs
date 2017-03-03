﻿using Rubberduck.Inspections.Abstract;
using System.Linq;
using Rubberduck.VBEditor;
using Rubberduck.Inspections.Resources;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Symbols;
using System.Windows.Forms;
using Rubberduck.UI.Refactorings;
using Rubberduck.Common;
using Antlr4.Runtime;
using System.Collections.Generic;
using Rubberduck.Parsing.VBA;

namespace Rubberduck.Inspections.QuickFixes
{
    public class AssignedByValParameterMakeLocalCopyQuickFix : QuickFixBase
    {
        private readonly Declaration _target;
        private readonly IAssignedByValParameterQuickFixDialogFactory _dialogFactory;
        private readonly RubberduckParserState _parserState;
        private readonly IEnumerable<string> _forbiddenNames;
        private string _localCopyVariableName;

        public AssignedByValParameterMakeLocalCopyQuickFix(Declaration target, QualifiedSelection selection, RubberduckParserState parserState, IAssignedByValParameterQuickFixDialogFactory dialogFactory)
            : base(target.Context, selection, InspectionsUI.AssignedByValParameterMakeLocalCopyQuickFix)
        {
            _target = target;
            _dialogFactory = dialogFactory;
            _parserState = parserState;
            _forbiddenNames = GetIdentifierNamesAccessibleToProcedureContext();
           _localCopyVariableName = ComputeSuggestedName();
        }

        public override bool CanFixInModule { get { return false; } }
        public override bool CanFixInProject { get { return false; } }

        public override void Fix()
        {
            RequestLocalCopyVariableName();

            if (!VariableNameIsValid(_localCopyVariableName) || IsCancelled)
            {
                return;
            }

            ReplaceAssignedByValParameterReferences();

            InsertLocalVariableDeclarationAndAssignment();
        }

        private void RequestLocalCopyVariableName()
        {
            using( var view = _dialogFactory.Create(_target.IdentifierName, _target.DeclarationType.ToString(), _forbiddenNames))
            {
                view.NewName = _localCopyVariableName;
                view.ShowDialog();
                IsCancelled = view.DialogResult == DialogResult.Cancel;
                if (!IsCancelled)
                {
                    _localCopyVariableName = view.NewName;
                }
            }
        }

        private string ComputeSuggestedName()
        {
            var newName = "local" + _target.IdentifierName.CapitalizeFirstLetter();
            if (VariableNameIsValid(newName))
            {
                return newName;
            }

            for ( var attempt = 2; attempt < 10; attempt++)
            {
                var result = newName + attempt;
                if (VariableNameIsValid(result))
                {
                    return result;
                }
            }
            return newName;
        }

        private bool VariableNameIsValid(string variableName)
        {
            return VariableNameValidator.IsValidName(variableName)
                && !_forbiddenNames.Any(name => name.Equals(variableName, System.StringComparison.InvariantCultureIgnoreCase));
        }

        private void ReplaceAssignedByValParameterReferences()
        {
            var module = Selection.QualifiedName.Component.CodeModule;
            foreach (var identifierReference in _target.References)
            {
                module.ReplaceIdentifierReferenceName(identifierReference, _localCopyVariableName);
            }
        }

        private void InsertLocalVariableDeclarationAndAssignment()
        { 
            string[] lines = { BuildLocalCopyDeclaration(), BuildLocalCopyAssignment() };
            var module = Selection.QualifiedName.Component.CodeModule;
            module.InsertLines(((VBAParser.ArgListContext)_target.Context.Parent).Stop.Line+1, lines);
        }

        private string BuildLocalCopyDeclaration()
        {
            return Tokens.Dim + " " + _localCopyVariableName + " " + Tokens.As + " " + _target.AsTypeName;
        }

        private string BuildLocalCopyAssignment()
        {
            return (_target.AsTypeDeclaration is ClassModuleDeclaration ? Tokens.Set + " " : string.Empty) 
                + _localCopyVariableName + " = " + _target.IdentifierName;
        }

        private IEnumerable<string> GetIdentifierNamesAccessibleToProcedureContext()
        {
            var allSameProcedureDeclarations = _parserState.AllUserDeclarations
                    .Where(item => item.ParentScope == _target.ParentScope)
                    .ToList();

            var sameModuleDeclarations = _parserState.AllUserDeclarations
                    .Where(item => item.ComponentName == _target.ComponentName
                    && !IsDeclaredInMethodOrProperty(item.ParentDeclaration.Context))
                    .ToList();

            var allGloballyAccessibleDeclarations = _parserState.AllUserDeclarations
                .Where(item => item.ProjectName == _target.ProjectName
                && !(item.ParentScopeDeclaration is ClassModuleDeclaration)
                && (item.Accessibility == Accessibility.Public 
                    || ((item.Accessibility == Accessibility.Implicit) 
                        && item.ParentScopeDeclaration is ProceduralModuleDeclaration)))
                .ToList();

            var accessibleIdentifierNames = new List<string>();
            accessibleIdentifierNames.AddRange(allSameProcedureDeclarations.Select(d => d.IdentifierName));
            accessibleIdentifierNames.AddRange(sameModuleDeclarations.Select(d => d.IdentifierName));
            accessibleIdentifierNames.AddRange(allGloballyAccessibleDeclarations.Select(d => d.IdentifierName));

            return accessibleIdentifierNames.Distinct();
        }

        private bool IsDeclaredInMethodOrProperty(RuleContext context)
        {
            if (context is VBAParser.SubStmtContext)
            {
                return true;
            }
            else if (context is VBAParser.FunctionStmtContext)
            {
                return true;
            }
            else if (context is VBAParser.PropertyLetStmtContext)
            {
                return true;
            }
            else if (context is VBAParser.PropertyGetStmtContext)
            {
                return true;
            }
            else if (context is VBAParser.PropertySetStmtContext)
            {
                return true;
            }
            return false;
        }
    }
}
