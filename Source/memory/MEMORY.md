# Project Memory Index

## Core Documentation
- [COMPATIBILITY_REPORT.md](COMPATIBILITY_REPORT.md) — 24-point FDS6 input compatibility checklist, all implemented
- [CUTFACE_IMPLEMENTATION.md](CUTFACE_IMPLEMENTATION.md) — Cut-face Variant C Lite, ALPHA_CF_W, R15 SUB_CELL_THIN restoration
- [R15_SUBCELL_THIN_FIX.md](R15_SUBCELL_THIN_FIX.md) — R15 fix: restored SUB_CELL_THIN, clip-first, multi-mesh OBST
- [CODER_HANDOVER_PROMPT.md](CODER_HANDOVER_PROMPT.md) — General project handover document
- [MLR_BASED_HRR_GUIDE.md](MLR_BASED_HRR_GUIDE.md) — MLR-based HRR calculation guide

## Implementation Details
- [MLR_BASED_HRR_IMPLEMENTATION.md](MLR_BASED_HRR_IMPLEMENTATION.md) — MLR HRR and CCC system technical details
- [MLR_ONLY_IMPLEMENTATION.md](MLR_ONLY_IMPLEMENTATION.md) — MLR-only mode (not implemented)
- [SOAP_IMPLEMENTATION.md](SOAP_IMPLEMENTATION.md) — SOAP pressure solver implementation
- [FDS5_SOAP_AND_PRESSURE_BLENDING.md](FDS5_SOAP_AND_PRESSURE_BLENDING.md) — Adaptive pressure blending, pressure floor
- [FDS5_MLR_BASED_HRR_AND_CCC_IMPLEMENTATION.md](FDS5_MLR_BASED_HRR_AND_CCC_IMPLEMENTATION.md) — Two CCC modes (fixed/procedural)
- [FDS5_INT_OVERFLOW_GUARDS.md](FDS5_INT_OVERFLOW_GUARDS.md) — 23 INT overflow fixes for large meshes
- [PHASE4_GRADIENT_CORRECTION.md](PHASE4_GRADIENT_CORRECTION.md) — Phase 4: 2nd-order gradient correction for multi-mesh interpolation
- [PHASE5_NESTED_MESH_PROMPT.md](PHASE5_NESTED_MESH_PROMPT.md) — Phase 5: Nested mesh refinement prompt for next coder

## Compatibility Plans
- [FDS5_FDS6_COMPATIBILITY_PLAN.md](FDS5_FDS6_COMPATIBILITY_PLAN.md) — 25-point SMV/Fenix+ compatibility (all done)
- [FDS5_SMV_COMPATIBILITY_PLAN.md](FDS5_SMV_COMPATIBILITY_PLAN.md) — SmokeView .smv format compatibility
- [FDS5_HRR_CSV_COMPATIBILITY.md](FDS5_HRR_CSV_COMPATIBILITY.md) — HRR/ctrl CSV format compatibility
- [FDS5_DERIVED_SPECIES_SLCF.md](FDS5_DERIVED_SPECIES_SLCF.md) — Derived species SLCF support
- [FDS5_DIVERGENCE_ADVECTION_TO_FDS6.md](FDS5_DIVERGENCE_ADVECTION_TO_FDS6.md) — Divergence advection port

## Performance
- [FDS5_SPEED_RESTORATION_PLAN.md](FDS5_SPEED_RESTORATION_PLAN.md) — Speed optimization plan
- [FDS5_SPEED_RESTORATION_PLAN_PHASE2.md](FDS5_SPEED_RESTORATION_PLAN_PHASE2.md) — Phase 2 optimizations
- [FDS5_SPEED_RESTORATION_PLAN_PHASE3.md](FDS5_SPEED_RESTORATION_PLAN_PHASE3.md) — Phase 3 optimizations
- [FDS6_SPEED_INVESTIGATION_PLAN.md](FDS6_SPEED_INVESTIGATION_PLAN.md) — FDS6 speed investigation
- [tier6_1_solid_array_optimization.md](tier6_1_solid_array_optimization.md) — SOLID array optimization
- [tier7_1_ghost_cell_optimization.md](tier7_1_ghost_cell_optimization.md) — Ghost cell optimization

## Chemistry & HRR
- [How_HRR_is_calculated_originaly.md](How_HRR_is_calculated_originaly.md) — Original HRR calculation
- [How_HRR_must_be_calculated.md](How_HRR_must_be_calculated.md) — Required HRR calculation
- [How_CCC_is_calculated_originaly.md](How_CCC_is_calculated_originaly.md) — Original CCC calculation
- [How_MLR_is_calculated_originaly.md](How_MLR_is_calculated_originaly.md) — Original MLR calculation
- [Cl-HCL_integration_in_FDS_Chemistry.md](Cl-HCL_integration_in_FDS_Chemistry.md) — Cl/HCl chemistry integration
- [INTEGRATED_INTENSITY_to_INCIDENT_HEAT_FLUX_Proof.md](INTEGRATED_INTENSITY_to_INCIDENT_HEAT_FLUX_Proof.md) — BNDF quantity conversion proof

## GUI Development
- [GUI_DEVELOPMENT_PROMPT.md](GUI_DEVELOPMENT_PROMPT.md) — FDS Building Editor GUI: архитектура, domain models, план фаз 0-9. Фазы 0-1 завершены (2026-06-13)

## Other
- [NEXT_STEPS.md](NEXT_STEPS.md) — Next steps planning
- [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) — Project context overview
- [HANDOVER_REPORT.md](HANDOVER_REPORT.md) — Handover report
- [DT.md](DT.md) — Time step documentation
- [LAZY_SOAP_MANUAL.md](LAZY_SOAP_MANUAL.md) — Lazy SOAP solver manual
- [bbfrac_modification_plan.md](bbfrac_modification_plan.md) — BBFRAC modification plan
- [bbfrac_modification_plan_2.md](bbfrac_modification_plan_2.md) — BBFRAC modification plan 2
- [bbfrac_modification_report.md](bbfrac_modification_report.md) — BBFRAC modification report
