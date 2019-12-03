﻿using Rubberduck.Parsing.Rewriter;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.SmartIndenter;
using Rubberduck.VBEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rubberduck.Refactorings.EncapsulateField.Strategies
{
    public interface IEncapsulateWithBackingUserDefinedType : IEncapsulateFieldStrategy
    {
        IEncapsulateFieldCandidate StateUDTField { set; get; }
    }

    public class EncapsulateWithBackingUserDefinedType : EncapsulateFieldStrategiesBase, IEncapsulateWithBackingUserDefinedType
    {
        public EncapsulateWithBackingUserDefinedType(QualifiedModuleName qmn, IIndenter indenter, IEncapsulateFieldNamesValidator validator)
            : base(qmn, indenter, validator) { }

        protected override IExecutableRewriteSession RefactorRewrite(EncapsulateFieldModel model, IExecutableRewriteSession rewriteSession, bool asPreview)
        {
            var stateUDTField_UDTMembers = model.UDTFieldCandidates
                    .Where(c => c.EncapsulateFlag || c.SelectedMembers.Any());

            foreach (var field in model.FieldCandidates)
            {
                if (field is IEncapsulatedUserDefinedTypeField udt)
                {
                    udt.FieldAccessExpression =
                        () =>   {
                                    var accessor = udt.EncapsulateFlag || stateUDTField_UDTMembers.Contains(udt) ? udt.PropertyName : udt.NewFieldName;
                                    return $"{StateUDTField.FieldAccessExpression()}.{accessor}";
                                };

                    foreach (var member in udt.Members)
                    {
                        member.FieldAccessExpression = () => $"{udt.FieldAccessExpression()}.{member.PropertyName}";
                    }
                }
                else
                {
                    var efd = field;
                    efd.FieldAccessExpression = () => $"{StateUDTField.FieldAccessExpression()}.{efd.PropertyName}";
                }
            }

            var fieldsToModify = model.EncapsulationFields
                    .Where(encFld => !encFld.IsUDTMember && encFld.EncapsulateFlag).Union(stateUDTField_UDTMembers);

            foreach (var field in fieldsToModify)
            {
                var attributes = field.EncapsulationAttributes;
                ModifyEncapsulatedVariable(field, attributes, rewriteSession);
                RenameReferences(field, attributes.PropertyName ?? field.Declaration.IdentifierName, rewriteSession);
            }

            var rewriter = EncapsulateFieldRewriter.CheckoutModuleRewriter(rewriteSession, TargetQMN);
            RewriterRemoveWorkAround.RemoveDeclarationsFromVariableLists(rewriter);

            InsertNewContent(model.CodeSectionStartIndex, model, rewriteSession, asPreview);

            return rewriteSession;
        }

        public IEncapsulateFieldCandidate StateUDTField { set; get; }

        protected override void ModifyEncapsulatedVariable(IEncapsulateFieldCandidate target, IFieldEncapsulationAttributes attributes, IRewriteSession rewriteSession)
        {
            var rewriter = EncapsulateFieldRewriter.CheckoutModuleRewriter(rewriteSession, TargetQMN);

            RewriterRemoveWorkAround.Remove(target.Declaration, rewriter);
            //rewriter.Remove(target.Declaration);
            return;
        }

        protected override EncapsulateFieldNewContent LoadNewDeclarationsContent(EncapsulateFieldNewContent newContent, IEnumerable<IEncapsulateFieldCandidate> encapsulationCandidates)
        {
            var udt = new UDTDeclarationGenerator(StateUDTField.AsTypeName);

            var stateUDTMembers = encapsulationCandidates
                .Where(encFld => !encFld.IsUDTMember
                    && (encFld.EncapsulateFlag
                        || encFld is IEncapsulatedUserDefinedTypeField udtFld && udtFld.Members.Any(m => m.EncapsulateFlag)));

            udt.AddMembers(stateUDTMembers);

            newContent.AddDeclarationBlock(udt.TypeDeclarationBlock(Indenter));

            newContent.AddDeclarationBlock(udt.FieldDeclaration(StateUDTField.NewFieldName));

            return newContent;
        }
    }
}
