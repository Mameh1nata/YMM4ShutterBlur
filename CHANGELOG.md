# Changelog

## 0.2.0 - 2026-09-15

- Added a 1–4 frame sampling interval for duplicated-frame and low-cadence sources.
- Added fractional exposure values in 0.1 frame steps.
- Added an adjustable 0–100% linear falloff.
- Changed the default mix for newly added effects from 100% to 70%.
- Preserved the v0.1.0 effect type and existing serialized properties for project compatibility.
- Expanded dynamic frame history to support spaced sampling, with a maximum of 29 cached frames.

## 0.1.0 - 2026-09-15

- Initial experimental release.
- Added normalized temporal frame accumulation, equal/linear weights, mix, and frame holding.
