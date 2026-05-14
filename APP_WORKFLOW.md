# LaserCollisionIn3DObjects – Application Workflow Documentation

## Tabs Overview

## 1) Collision Workspace Tab
This tab is used to define 3D scene elements and execute ray-prism collision computations.

### Workflow
1. Add prisms manually or in array placement mode.
2. Add manual rays and/or cylindrical light sources.
3. Run collision using selected algorithm (sequential or parallel).
4. Inspect hit list and timing metrics.

## 2) Annotation Verification Tab
This tab is used to verify VIA annotations, fit panel corners, rectify panel images, and inspect hole centers in pixel and millimeter domains.

### Workflow
1. Select a folder containing VIA JSON and image files.
2. Navigate images.
3. Review fitted panel corners on original image.
4. Review warped panel and hole centers.
5. Enter panel dimensions (mm):
   - Global width/height can be applied to all images.
   - Per-image width/height can then be adjusted individually.
6. Hole centers are converted from warped pixel coordinates to millimeters using:
   - `x_mm = x_px * panel_width_mm / warped_width_px`
   - `y_mm = y_px * panel_height_mm / warped_height_px`

## Annotation Metric Conversion Algorithm
Given a rectified panel image and user-provided panel dimensions:
- Origin remains top-left.
- `x` axis points right.
- `y` axis points down.
- Pixel-to-mm scale is anisotropic (independent x/y scales):
  - `sx = panel_width_mm / warped_image_width_px`
  - `sy = panel_height_mm / warped_image_height_px`
- Each warped hole center is converted using `(x_px*sx, y_px*sy)`.

## Accessing metric hole centers from app state
Metric-converted hole centers are exposed through annotation view-model state so other app features (including collision-related extensions) can consume them.
