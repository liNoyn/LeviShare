# Interface notes

LeviShare's Avalonia window uses [LeviLauncher](https://github.com/LiteLDev/LeviLauncher)'s look as a visual reference only: the zinc light/dark surfaces and emerald accent from `frontend/src/style.css`, and the compact list rows of `ModsPage.tsx`. No LeviLauncher code or assets are included (LeviLauncher is GPL-3.0; LeviShare is MIT), and LeviShare uses its own icon so it is not mistaken for an official LiteLDev tool.

Theme brushes are in `App.axaml` (Light / Dark theme dictionaries). Switching language or theme keeps the current selection.
