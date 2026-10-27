# Inventor Model Shoot

Autodesk Inventor 2027 add-in (VB.NET, .NET 8) that captures screenshots of the active
model and sends them by email.

The **Model Shoot** button appears in the **Inventor Model Shoot** panel on the existing
**Tools** tab, in both the Part and Assembly environments.

## Supported Inventor versions

Inventor 2025 and newer (2025, 2026, 2027 and later). The project compiles against the Inventor 2025
interop (`InteropVersion` in the project file). The interop is embedded, so the same DLL runs on newer
versions. The `.addin` file only loads in Inventor 2025 or later, because older versions run on
.NET Framework and cannot load a .NET 8 add-in.

Avoid API members that were renamed or removed in newer versions (for example `Document.DisplayName`,
which became `DisplayNameBasis` in 2027). Check members against the interop XML of the oldest and the
newest version before using them.

## Build and install

Build in Visual Studio with Inventor closed. The post-build target copies the add-in bundle to
`%AppData%\Autodesk\ApplicationPlugins\Inventor_Model_Shoot.bundle`, which every supported Inventor version
reads. Cleaning the project removes it. Press F5 to start the Inventor version named in
`DebugInventorVersion`.
