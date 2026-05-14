"""Fusion 360 script: adjustable fingertip mount for a 3x3 flexible pressure sensor.

Run from Fusion 360: Utilities > Scripts and Add-Ins > Scripts > + > select this file.

The script creates a new component with editable user parameters. To resize it,
change the parameters in Modify > Change Parameters, then run the script again.
It will replace the previous generated component.
"""

import traceback

import adsk.core
import adsk.fusion


COMPONENT_NAME = "Adjustable Finger Sensor Mount"


DEFAULTS_MM = {
    # Fit
    "finger_diameter": (18.0, "Inside finger diameter at distal phalanx"),
    "wall_thickness": (1.4, "Cuff wall thickness"),
    "band_length": (18.0, "Length of fingertip sleeve along the finger"),
    "top_gap_width": (10.0, "Opening width at the top of the flexible cuff"),
    # Sensor dimensions from the supplied 3x3 multipoint sensor drawing.
    "sensor_head_width": (14.0, "Square sensing head width"),
    "sensor_head_length": (14.0, "Square sensing head length"),
    "sensor_tail_width": (7.0, "Narrow flexible tail width"),
    "reinforcement_width": (11.0, "Wider reinforced tail section width"),
    "sensor_thickness": (0.3, "Sensor stack thickness"),
    "sensor_clearance": (0.8, "Extra side clearance around sensor features"),
    "sensor_head_from_tip": (1.0, "Distance from fingertip-side cuff edge to sensor head"),
    "sensor_contact_preload": (0.1, "How far the adhesive bed face protrudes into the finger opening"),
    "adhesive_bed_thickness": (0.45, "Printed flat backing thickness behind the adhesive side"),
    "cell_size": (3.5, "Individual pressure cell size"),
    "cell_pitch": (4.0, "Pressure cell center-to-center pitch"),
}

LEGACY_DEFAULTS_MM = {
    "band_length": (20.0, 22.0),
    "wall_thickness": (1.7, 2.2),
    "sensor_contact_preload": (0.2,),
    "adhesive_bed_thickness": (0.9,),
}


def mm(value):
    """Fusion API length values are centimeters."""
    return value / 10.0


def get_param_mm(design, name, default_mm, comment):
    params = design.userParameters
    param = params.itemByName(name)
    if param is None:
        param = params.add(
            name,
            adsk.core.ValueInput.createByString(f"{default_mm} mm"),
            "mm",
            comment,
        )
    elif name in LEGACY_DEFAULTS_MM:
        value_mm = param.value * 10.0
        for legacy_mm in LEGACY_DEFAULTS_MM[name]:
            if abs(value_mm - legacy_mm) < 0.001:
                param.expression = f"{default_mm} mm"
                break
    return param.value * 10.0


def read_params(design):
    return {
        name: get_param_mm(design, name, value, comment)
        for name, (value, comment) in DEFAULTS_MM.items()
    }


def delete_existing(root):
    for index in range(root.occurrences.count - 1, -1, -1):
        occurrence = root.occurrences.item(index)
        if occurrence.component.name == COMPONENT_NAME:
            occurrence.deleteMe()


def add_rect(sketch, x1, y1, x2, y2):
    return sketch.sketchCurves.sketchLines.addTwoPointRectangle(
        adsk.core.Point3D.create(mm(x1), mm(y1), 0),
        adsk.core.Point3D.create(mm(x2), mm(y2), 0),
    )


def largest_profile(sketch):
    best = None
    best_span = -1.0
    for index in range(sketch.profiles.count):
        profile = sketch.profiles.item(index)
        bounds = profile.boundingBox
        span_x = bounds.maxPoint.x - bounds.minPoint.x
        span_y = bounds.maxPoint.y - bounds.minPoint.y
        span = max(span_x, span_y)
        if span > best_span:
            best = profile
            best_span = span
    return best


def extrude(comp, profile, distance_mm, operation, participant=None, symmetric=False):
    extrudes = comp.features.extrudeFeatures
    ext_input = extrudes.createInput(profile, operation)
    if symmetric:
        ext_input.setSymmetricExtent(
            adsk.core.ValueInput.createByReal(mm(abs(distance_mm))),
            True,
        )
    else:
        ext_input.setDistanceExtent(
            False,
            adsk.core.ValueInput.createByReal(mm(distance_mm)),
        )
    return extrudes.add(ext_input)


def offset_plane(comp, base_plane, offset_mm):
    planes = comp.constructionPlanes
    plane_input = planes.createInput()
    plane_input.setByOffset(
        base_plane,
        adsk.core.ValueInput.createByReal(mm(offset_mm)),
    )
    return planes.add(plane_input)


def add_box_from_xy(comp, name, x1, y1, x2, y2, z_mm, height_mm, operation, participant=None):
    plane = offset_plane(comp, comp.xYConstructionPlane, z_mm)
    sketch = comp.sketches.add(plane)
    add_rect(sketch, x1, y1, x2, y2)
    feature = extrude(comp, sketch.profiles.item(0), height_mm, operation, participant)
    for body in feature.bodies:
        body.name = name
    return feature


def add_box_from_xz(comp, name, x1, z1, x2, z2, y_mm, depth_mm, operation, participant=None):
    plane = offset_plane(comp, comp.xZConstructionPlane, y_mm)
    sketch = comp.sketches.add(plane)
    add_rect(sketch, x1, z1, x2, z2)
    feature = extrude(comp, sketch.profiles.item(0), depth_mm, operation, participant)
    for body in feature.bodies:
        body.name = name
    return feature


def build_model(comp, p):
    inner_r = p["finger_diameter"] / 2.0
    outer_r = inner_r + p["wall_thickness"]
    band = p["band_length"]
    head_w = p["sensor_head_width"]
    head_l = p["sensor_head_length"]
    clearance = p["sensor_clearance"]
    head_x1 = max(0.0, min(p["sensor_head_from_tip"], band - head_l))
    head_x2 = head_x1 + head_l
    head_clear_w = head_w + clearance
    head_window_x1 = max(0.0, head_x1 - clearance / 2.0)
    head_window_x2 = min(band, head_x2 + clearance / 2.0)
    adhesive_surface_y = inner_r - p["sensor_contact_preload"]
    sensor_contact_y = adhesive_surface_y - p["sensor_thickness"]

    # Main split cuff: annular profile on the YZ plane, extruded along X.
    yz = comp.yZConstructionPlane
    cuff_sketch = comp.sketches.add(yz)
    circles = cuff_sketch.sketchCurves.sketchCircles
    circles.addByCenterRadius(adsk.core.Point3D.create(0, 0, 0), mm(outer_r))
    circles.addByCenterRadius(adsk.core.Point3D.create(0, 0, 0), mm(inner_r))
    cuff_feature = extrude(
        comp,
        largest_profile(cuff_sketch),
        band,
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    cuff_body = cuff_feature.bodies.item(0)
    cuff_body.name = "print body - split cuff"

    # Dorsal opening for adjustability.
    gap_sketch = comp.sketches.add(yz)
    gap_half = p["top_gap_width"] / 2.0
    add_rect(gap_sketch, -gap_half, inner_r * 0.35, gap_half, outer_r + 2.0)
    extrude(
        comp,
        gap_sketch.profiles.item(0),
        band + 0.5,
        adsk.fusion.FeatureOperations.CutFeatureOperation,
        cuff_body,
    )

    # Flat inner side-wall adhesive bed. The sensor backing sticks to this face,
    # while the contact face protrudes slightly into the finger opening.
    bed_feature = add_box_from_xz(
        comp,
        "print body - inner sensor adhesive bed",
        head_window_x1,
        -head_clear_w / 2.0,
        head_window_x2,
        head_clear_w / 2.0,
        adhesive_surface_y,
        p["adhesive_bed_thickness"],
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
        cuff_body,
    )
    for body in bed_feature.bodies:
        body.name = "print body - inner sensor adhesive bed"

    # Thin sensor reference bodies. Suppress or hide before exporting the printable body.
    sensor_y = sensor_contact_y
    head_feature = add_box_from_xz(
        comp,
        "reference only - 14 mm sensing head",
        head_x1,
        -head_w / 2.0,
        head_x2,
        head_w / 2.0,
        sensor_y,
        p["sensor_thickness"],
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    head_feature.bodies.item(0).name = "reference only - 14 mm sensing head"

    tail_feature = add_box_from_xz(
        comp,
        "reference only - 7 mm flexible tail",
        head_x2,
        -p["sensor_tail_width"] / 2.0,
        band + 45.0,
        p["sensor_tail_width"] / 2.0,
        sensor_y,
        p["sensor_thickness"],
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    tail_feature.bodies.item(0).name = "reference only - 7 mm flexible tail"

    reinforcement_feature = add_box_from_xz(
        comp,
        "reference only - 11 mm reinforcement",
        band + 17.0,
        -p["reinforcement_width"] / 2.0,
        band + 45.0,
        p["reinforcement_width"] / 2.0,
        sensor_y + p["sensor_thickness"],
        p["sensor_thickness"],
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    reinforcement_feature.bodies.item(0).name = "reference only - 11 mm reinforcement"

    cell_y = sensor_y - 0.08
    first_cell_x = head_x1 + (head_l - 2.0 * p["cell_pitch"]) / 2.0
    first_cell_y = -p["cell_pitch"]
    for row in range(3):
        for col in range(3):
            center_x = first_cell_x + col * p["cell_pitch"]
            center_y = first_cell_y + row * p["cell_pitch"]
            cell_feature = add_box_from_xz(
                comp,
                "reference only - pressure cell",
                center_x - p["cell_size"] / 2.0,
                center_y - p["cell_size"] / 2.0,
                center_x + p["cell_size"] / 2.0,
                center_y + p["cell_size"] / 2.0,
                cell_y,
                0.08,
                adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
            )
            cell_feature.bodies.item(0).name = f"reference only - pressure cell {row + 1}-{col + 1}"


def run(context):
    ui = None
    try:
        app = adsk.core.Application.get()
        ui = app.userInterface
        design = adsk.fusion.Design.cast(app.activeProduct)
        if design is None:
            ui.messageBox("Open or create a Fusion 360 design before running this script.")
            return

        design.designType = adsk.fusion.DesignTypes.ParametricDesignType
        root = design.rootComponent
        params = read_params(design)
        delete_existing(root)

        occurrence = root.occurrences.addNewComponent(adsk.core.Matrix3D.create())
        comp = occurrence.component
        comp.name = COMPONENT_NAME
        build_model(comp, params)

        ui.messageBox(
            "Created elastic-retained finger sensor mount.\n\n"
            "Change dimensions in Modify > Change Parameters, then rerun this script.\n"
            "Print the wearable body in TPU or another flexible material.\n"
            "Hide all 'reference only' sensor bodies before exporting STL/3MF."
        )
    except Exception:
        if ui:
            ui.messageBox("Script failed:\n{}".format(traceback.format_exc()))


def stop(context):
    pass
