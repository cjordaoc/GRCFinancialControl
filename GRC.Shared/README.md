# GRC Shared
GRC Apps Shared Content

## Shared UI Controls

### Modal Dialogs
Modal dialogs are created through `IModalDialogService` using `ModalDialogOptions`.

**Available options:**
- `Layout` (`ModalDialogLayout`): `CenteredOverlay` or `OwnerAligned`.
- `SizeRatioResourceKey`: Resource key for a tokenized dialog size ratio (defaults to `DialogContentRatioStandard`).
- `ContentSizeRatio`: Numeric fallback when `SizeRatioResourceKey` is null.
- `Title`: Overrides the dialog window title when the call does not pass a title.
- `ShowWindowControls`: Shows or hides system window chrome (close button/title bar).
- `DimBackground`: Enables or disables the dimmed glass overlay.
- `FreezeOwner`: Enables or disables freezing the owner window while the dialog is open.
- `ContainerMargin`: Optional padding around dialog content.

**Example:**
Create options for a compact dialog that keeps the owner interactive:
- `SizeRatioResourceKey = "DialogContentRatioCompact"`
- `ShowWindowControls = false`
- `DimBackground = true`
- `FreezeOwner = false`

The dialog service enforces fail-fast resource lookups for size tokens and brushes, so missing keys will throw.
