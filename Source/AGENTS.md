# AGENTS.md — Guide for AI Coding Agents

This file is written in English on purpose: it targets third-party AI agents that
assist with code modifications. All other project documents are in Russian.

## 1. Repository identity

`fds_excessive_plus` is a fire dynamics solver built on FDS 5.5.3 (code revision
**R28e**). The upstream lineage is [firemodels/fds](https://github.com/firemodels/fds)
(merge-base `5c3e6e20e6`). The project converts FDS 5 into a two-mode fast solver
for fire-safety engineering calculations following Russian regulatory methodology
(Order No. 1140, Annex 1 prescribed heat release curves).

Licensing: the base FDS code is NIST public-domain software (see `LICENSE.md`);
the modifications of this fork are distributed under GPL-3.0 (see `LICENSE`).

## 2. Repository layout

| Path | Contents |
|---|---|
| `Source/fds5/` | **The solver.** FDS 5.5.3 core with all fork modifications (revision R28e). |
| `Source/*.f90` | FDS6 source tree with local modifications (Fuel Wizard launcher in `main.f90`, vegetation stub in `vege.f90`, threaded `CC_SCALARS` variants). Auxiliary tree; it is not the production solver. |
| `Build/makefile5` | Makefile for the FDS5 solver. |
| `Build/impi_intel_win/` | Windows build scripts: `make_fds5.bat` (release), `make_fds5_debug.bat` (debug). |
| `Build/impi_intel_win_db/` | Debug build script variant. |

The directories `Manuals/`, `Verification/`, `Validation/`, and `Utilities/` of the
upstream repository are excluded from this publication.

Key source files of the solver (`Source/fds5/`):

- `main.f90` — program entry, main time loop;
- `read.f90` — input file reader and the FDS6-compatibility conversion layer;
- `cons.f90` — global constants, parameters, defaults (revision-tagged `R<n><letter>` comments);
- `fire.f90` — combustion: EDC model and the prescribed-profile mode (`COMBUSTION_FLAME_SHEET`);
- `velo.f90` — velocity update, Courant norm, time-step controller `CHECK_STABILITY`;
- `pois.f90` — pressure solvers (FFT, SOAP with extrapolation and smoothing);
- `dump.f90` — output formats compatible with SmokeView;
- `dump.f90`, `read.f90`, `cons.f90` jointly implement the FDS6/Fenix+ compatibility layer.

Revision history is encoded in comments as `R20`, `R26b`, `R28e`, … Use a new
revision tag for every non-trivial change and keep the tag consistent across files
touched by one change.

## 3. Two heat-release modes

1. **Prescribed-profile mode** (`FLAME_SHEET=.TRUE.` on `&MISC`), for coarse meshes
   with cell size ≥ 0.5 m. Heat release rate follows the prescribed `RAMP_Q` (or
   `TAU_Q`) curve exactly and is distributed over a conical source volume bounded
   by a volumetric heat release density cap.
2. **Full EDC combustion** (default), for fine meshes ≤ 0.25 m. Mixing-limited
   reaction rate with the local volumetric heat release limit (default 2500 kW/m³,
   inherited from base FDS5).

Known applicability boundary: on 0.25 m meshes the prescribed-profile mode loses
interfacial stability between gas layers when the heat release rate approaches the
plateau; use full EDC combustion at that resolution.

## 4. Key namelist parameters

| Parameter | Group | Default | Meaning |
|---|---|---|---|
| `FLAME_SHEET` | `&MISC` | `.FALSE.` | Enable prescribed-profile heat release. |
| `FLAME_SHEET_QCAP` | `&MISC` | 500 kW/m³ | Upper bound of volumetric heat release density in the source volume. |
| `FLAME_SHEET_HMAX` | `&MISC` | 0 m | Upper bound of source volume height; 0 disables the bound. |
| `FLAME_SHEET_TAU` | `&MISC` | 0 s | Thermal buffering time constant of the source cone; 0 disables. |
| `AUTO_ZONE` | `&MISC` | `.FALSE.` | Automatic zoning of sealed volumes. |
| `STRICT_POCKETS` | `&MISC` | `.TRUE.` | Zoning of trapped gas pockets. |
| `CFL_DP_SCALE` | — (code constant) | 1.0 | Weight of the divergence term in the Courant norm. |

Time-step controller: admissible Courant number band 0.8–1.0; on exceedance the
step is multiplied by 0.9 and the predictor half-step is repeated; below 0.8 the
step is multiplied by 1.5.

## 5. Compatibility layer (FDS6 / Fenix+ / SmokeView)

Implemented mainly in `read.f90` and `dump.f90`:

- reading, conversion, and controlled ignoring of FDS6 input constructs;
- lumped `AIR`/`PRODUCTS` species; `SPEC_ID_NU`/`NU` reaction specification;
- FDS6 `DEVC`/`CTRL` quantities; `EXTERNAL` control functions with CSV input;
- Fenix+ 3.7.1.1 exports: `PROC_CTRL` in `read.f90` iterates `INPUT_ID` with a
  bounded loop `DO NN=1,SIZE(CF%INPUT_ID)` (fixes the crash on `INPUT_ID(1:40)`).
- output files remain readable by Smokeview and FDS6 post-processing tools.

## 6. Build and run (Windows)

```
cmd /c "cd /d <repo>\Build\impi_intel_win && make_fds5.bat"
```

Toolchain: Intel oneAPI (ifx 2025.3.0), Intel MPI, MKL. After test runs,
terminate leftover `fds5_*` processes so they do not occupy cores.

## 7. Rules for agents

1. Any performance claim must be backed by a mirror comparison against the
   original FDS6 on the same scenario; do not state unverified speedups.
2. Code comments are written in Russian; keep them minimal and precise.
3. Keep FDS6/Fenix+ input compatibility: any new input handling must degrade
   gracefully for plain FDS5 input files.
4. After changing the solver, rebuild and run at least one scenario end-to-end
   before reporting completion.
