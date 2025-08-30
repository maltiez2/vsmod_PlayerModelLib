// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "<Pending>", Scope = "member", Target = "~F:PlayerModelLib.GuiDialogCreateCustomCharacter._clientSelectionDone")]
[assembly: SuppressMessage("Minor Code Smell", "S6602:\"Find\" method should be used instead of the \"FirstOrDefault\" extension", Justification = "<Pending>", Scope = "member", Target = "~M:PlayerModelLib.GuiDialogCreateCustomCharacter.OnRandomizeSkin(System.Collections.Generic.Dictionary{System.String,System.String})~System.Boolean")]
[assembly: SuppressMessage("Minor Code Smell", "S3400:Methods should not return constants", Justification = "Required for harmony patch", Scope = "member", Target = "~M:PlayerModelLib.OtherPatches.ReloadSkin~System.Boolean")]
[assembly: SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "<Pending>", Scope = "type", Target = "~T:PlayerModelLib.OtherPatches.EntityBehaviorContainerPatchCommand")]
