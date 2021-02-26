﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Moq;
using Rubberduck.Parsing.Rewriter;
using Rubberduck.VBEditor.SafeComWrappers;
using RubberduckTests.Mocks;
using Rubberduck.Refactorings.EncapsulateFieldUseBackingField;
using Rubberduck.Parsing.VBA;
using Rubberduck.Refactorings.EncapsulateField;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Refactorings;
using Rubberduck.SmartIndenter;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

namespace RubberduckTests.Refactoring.EncapsulateField
{
    [TestFixture]
    public class EncapsulateFieldReferenceReplacerValueFieldTests
    {
        [TestCase(true)]
        [TestCase(false)]
        [Category("Refactorings")]
        [Category("Encapsulate Field")]
        [Category(nameof(EncapsulateFieldReferenceReplacer))]
        public void PublicField_ExternalReference(bool wrapInPrivateUDT)
        {
            var target = "targetField";
            var propertyName = "MyProperty";
            var testTargetTuple = (target, propertyName, false);

            var testModuleName = MockVbeBuilder.TestModuleName;
            var referenceExpression = $"{testModuleName}.{target}";
            var testModuleCode =
$@"
Option Explicit
Public targetField As Long";

            var declaringModule = (testModuleName, testModuleCode, ComponentType.StandardModule);

            var procedureModuleReferencingCode =
$@"
Option Explicit

Public Sub Bar()
    {referenceExpression} = 7
End Sub
";
            var referencingModuleStdModule = (moduleName: "StdModule", procedureModuleReferencingCode, ComponentType.StandardModule);

            var refactoredCode = TestReferenceReplacement(wrapInPrivateUDT, testTargetTuple, declaringModule, referencingModuleStdModule);

            var referencingModuleCode = refactoredCode[referencingModuleStdModule.moduleName];

            StringAssert.Contains($"{testModuleName}.{propertyName} = ", referencingModuleCode);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("Refactorings")]
        [Category("Encapsulate Field")]
        [Category(nameof(EncapsulateFieldReferenceReplacer))]
        public void PublicField_ExternalWithMemberAccessReference(bool wrapInPrivateUDT)
        {
            var target = "targetField";
            var propertyName = "MyProperty";
            var testTargetTuple = (target, propertyName, false);

            var testModuleName = MockVbeBuilder.TestModuleName;
            var referenceExpression = $".{target}";
            var testModuleCode =
$@"
Option Explicit
Public targetField As Long";

            var declaringModule = (testModuleName, testModuleCode, ComponentType.StandardModule);

            var procedureModuleReferencingCode =
$@"
Option Explicit

Public Sub Bar()
    With {testModuleName}
        {referenceExpression} = 7
    End With
End Sub
";
            var referencingModuleStdModule = (moduleName: "StdModule", procedureModuleReferencingCode, ComponentType.StandardModule);

            var refactoredCode = TestReferenceReplacement(wrapInPrivateUDT, testTargetTuple, declaringModule, referencingModuleStdModule);

            var referencingModuleCode = refactoredCode[referencingModuleStdModule.moduleName];

            StringAssert.Contains($"  .{propertyName} = ", referencingModuleCode);
        }

        [TestCase(true, true, "Public")]
        [TestCase(false, true, "Public")]
        [TestCase(true, false, "Public")]
        [TestCase(false, false, "Public")]
        [TestCase(true, true, "Private")]
        [TestCase(false, true, "Private")]
        [TestCase(true, false, "Private")]
        [TestCase(false, false, "Private")]
        [Category("Refactorings")]
        [Category("Encapsulate Field")]
        [Category(nameof(EncapsulateFieldReferenceReplacer))]
        public void PublicPrivateField_LocalReferences(bool wrapInPrivateUDT, bool isReadOnly, string visibility)
        {
            var target = "targetField";
            var propertyName = "MyProperty";
            var testTargetTuple = (target, propertyName, isReadOnly);

            var testModuleName = MockVbeBuilder.TestModuleName;
            var testModuleCode =
$@"
Option Explicit
{visibility} {target} As Long

Public Sub Bar()
    {target} = 7
    Bars {target}
End Sub

Public Sub Bars(ByVal arg As Long)
End Sub
";

            var declaringModule = (testModuleName, testModuleCode, ComponentType.StandardModule);

            var refactoredCode = TestReferenceReplacement(wrapInPrivateUDT, testTargetTuple, declaringModule);

            var results = refactoredCode[testModuleName];

            var expectedAssignment = !isReadOnly
                ? $"{propertyName} = 7"
                : wrapInPrivateUDT ? $"this.{propertyName} = 7" : $"{target} = 7";

            StringAssert.Contains(expectedAssignment, results);
            StringAssert.Contains($" Bars {propertyName}", results);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        [Category("Refactorings")]
        [Category("Encapsulate Field")]
        [Category(nameof(EncapsulateFieldReferenceReplacer))]
        public void PublicField_OverrideReadOnlyFlagForExternalReferences(bool wrapInPrivateUDT, bool isReadOnly)
        {
            var target = "targetField";
            var propertyName = "MyProperty";

            //Simulates the scenario where the readOnly flag was set to 'True' by some means (other than the UI).
            //Since there are external references, a property Let/Set will be generated 
            //by the refactoring - so the references are modified accordingly.
            //So, the solution for both test cases are identical
            var testTargetTuple = (target, propertyName, isReadOnly);

            var testModuleName = MockVbeBuilder.TestModuleName;
            var testModuleCode =
$@"
Option Explicit

Public targetField As Long

Private mValue As Long

Public Sub Fizz(arg As Long)
    mValue = targetField + arg
End Sub

Public Sub Bizz(arg As Long)
    targetField = arg
End Sub
";
            var declaringModule = (testModuleName, testModuleCode, ComponentType.StandardModule);

            var referencingModule = "SomeOtherModule";
            var referencingModuleCode =
$@"
Option Explicit

Private mValue As Long

Public Sub Fazz(arg As Long)
    mValue = targetField * arg
End Sub

Public Sub Fazzle(arg As Long)
    targetField = arg * mValue
End Sub
";

            var referencingModuleStdModule = (moduleName: referencingModule, referencingModuleCode, ComponentType.StandardModule);

            var refactoredCode = TestReferenceReplacement(wrapInPrivateUDT, testTargetTuple, declaringModule, referencingModuleStdModule);

            StringAssert.Contains($"mValue = {propertyName} + arg", refactoredCode[MockVbeBuilder.TestModuleName]);
            StringAssert.Contains($"{propertyName} = arg", refactoredCode[MockVbeBuilder.TestModuleName]);

            StringAssert.Contains($"mValue = {MockVbeBuilder.TestModuleName}.{propertyName} * arg", refactoredCode[referencingModule]);
            StringAssert.Contains($"  {MockVbeBuilder.TestModuleName}.{propertyName} = arg * mValue", refactoredCode[referencingModule]);
        }

        [TestCase(true)]
        [TestCase(false)]
        [Category("Refactorings")]
        [Category("Encapsulate Field")]
         public void PublicFieldOfClassModule_ExternalReferences(bool wrapInPrivateUDT)
        {
            var target = "mFizz";
            var propertyName = "Fizz";

            var testTargetTuple = (target, propertyName, false);

            var testModuleName = MockVbeBuilder.TestModuleName;
            var testModuleCode =
@"Public mFizz As Integer";

            var declaringModule = (testModuleName, testModuleCode, ComponentType.ClassModule);

            var referencingModule = "SomeOtherModule";
            var referencingModuleCode =
$@"Sub Bazz()
    With new {testModuleName}
        .mFizz = 0
        Bar .mFizz
    End With
End Sub

Sub Bizz()
    Dim theClass As {testModuleName}
    Set theClass = new {testModuleName}
    theClass.mFizz = 0
    Bar theClass.mFizz
End Sub

Sub Bar(ByVal v As Integer)
End Sub";
            
            var referencingModuleStdModule = (moduleName: referencingModule, referencingModuleCode, ComponentType.StandardModule);

            var refactoredCode = TestReferenceReplacement(wrapInPrivateUDT, testTargetTuple, declaringModule, referencingModuleStdModule);

            StringAssert.Contains($".{propertyName} = 0", refactoredCode[referencingModule]);
            StringAssert.Contains($"Bar .{propertyName}", refactoredCode[referencingModule]);
            StringAssert.Contains($" theClass.{propertyName} = 0", refactoredCode[referencingModule]);
            StringAssert.Contains($"Bar theClass.{propertyName}", refactoredCode[referencingModule]);
            StringAssert.DoesNotContain("mFizz", refactoredCode[referencingModule]);
        }

        private static IDictionary<string, string> TestReferenceReplacement(bool wrapInPrivateUDT, (string, string, bool) testTargetTuple, params (string, string, ComponentType)[] moduleTuples)
        {
            return ReferenceReplacerTestSupport.TestReferenceReplacement(wrapInPrivateUDT, testTargetTuple, moduleTuples);
        }
    }
}
