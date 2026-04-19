# Bundled third-party software

## EqualizerAPO64.exe

AudioSwitch bundles the Equalizer APO installer to make first-time setup of
driver-level EQ a one-click experience. Equalizer APO is third-party software
distributed under the GNU General Public License version 2 (GPLv2).

| Field | Value |
|-------|-------|
| Project | Equalizer APO |
| Author | Jonas Thedering |
| Project home | https://sourceforge.net/projects/equalizerapo/ |
| Source code | https://sourceforge.net/p/equalizerapo/code/ |
| License | GNU GPL v2 — https://www.gnu.org/licenses/old-licenses/gpl-2.0.html |
| Bundled file | `EqualizerAPO64.exe` |
| Bundled size | 11,980,366 bytes (≈ 11.4 MB) |
| Bundled SHA-256 | `7403BE7427BBE1936A40DDED082829B6E217FC4F5990FEE5CBA501F0AE055AFA` |
| Source URL | https://sourceforge.net/projects/equalizerapo/files/latest/download |
| Downloaded on | 2026-04-19 |

### GPLv2 distribution requirements

Per GPLv2 §3, anyone who receives a copy of this binary is entitled to receive
the corresponding source code on the same medium or via a written offer. We
satisfy this by:

1. Pointing recipients at the upstream source repository (link above), which
   provides the complete corresponding source under the same license.
2. Not modifying the bundled binary — it is distributed verbatim from the
   upstream SourceForge release.

If you redistribute AudioSwitch in a form that includes this binary (a build
artifact, a portable zip, etc.), keep this NOTICES file alongside it.

### Why we bundle it

Equalizer APO needs to be installed at the driver/SFX layer (it registers an
Audio Processing Object with Windows) to actually filter audio. AudioSwitch
itself can write APO config files, but without APO present those files don't
reach the audio engine. Bundling the installer removes a "go to SourceForge,
download, run" step from first-time setup; users still see the standard APO
installer UI (admin prompt + Configurator dialog for picking which devices APO
hooks into) and still need to reboot for the APO driver to load.
