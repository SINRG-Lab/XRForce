"""Fusion 360 script: elastic fingertip cot for a 3x3 pressure sensor.

This is a fresh approach from the sketch: a thin TPU sleeve/cot with a flat
internal pad where the pressure sensor adhesive backing can stick. The sensor
contact face sits inside the sleeve and touches the fingertip when worn.

Run from Fusion 360: Utilities > Scripts and Add-Ins > Scripts > + > select
this file.
"""

import math
import traceback

import adsk.core
import adsk.fusion


COMPONENT_NAME = "Elastic Fingertip Pressure Sensor Cot"


DEFAULTS_MM = {
    # Wearable fit. Print in TPU/flexible resin and tune finger diameter first.
    "cot_finger_diameter": (18.0, "Inside fingertip diameter"),
    "cot_wall_thickness": (1.25, "Flexible sleeve wall thickness"),
    "cot_length": (18.0, "Sleeve length along the finger"),
    "cot_top_opening_angle": (95.0, "Nail-side opening angle in degrees"),
    # Sensor drawing dimensions.
    "cot_sensor_head_width": (14.0, "Square pressure sensor head width"),
    "cot_sensor_head_length": (14.0, "Square pressure sensor head length"),
    "cot_sensor_tail_width": (7.0, "Narrow flexible tail width"),
    "cot_reinforcement_width": (11.0, "Wider reinforced tail section width"),
    "cot_sensor_thickness": (0.3, "Pressure sensor thickness"),
    "cot_sensor_clearance": (0.7, "Extra side clearance around sensor"),
    # Sensor placement.
    "cot_sensor_from_tip": (1.5, "Distance from fingertip-side edge to sensor head"),
    "cot_sensor_preload": (0.1, "How far sensor contact face protrudes into finger space"),
    "cot_adhesive_pad_thickness": (0.55, "Printed internal backing pad thickness"),
    # Visual reference cells from the supplied drawing.
    "cot_cell_size": (3.5, "Individual pressure cell size"),
    "cot_cell_pitch": (4.0, "Pressure cell center-to-center pitch"),
}


def mm(value):
    """Fusion API length values are centimeters."""
    return value / 10.0


def point_yz(y_mm, z_mm):
    return adsk.core.Point3D.create(0, mm(y_mm), mm(z_mm))


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


def extrude(comp, profile, distance_mm, operation):
    extrudes = comp.features.extrudeFeatures
    ext_input = extrudes.createInput(profile, operation)
    ext_input.setDistanceExtent(False, adsk.core.ValueInput.createByReal(mm(distance_mm)))
    return extrudes.add(ext_input)


def offset_plane(comp, base_plane, offset_mm):
    planes = comp.constructionPlanes
    plane_input = planes.createInput()
    plane_input.setByOffset(base_plane, adsk.core.ValueInput.createByReal(mm(offset_mm)))
    return planes.add(plane_input)


def add_rect_xy(sketch, x1, y1, x2, y2):
    return sketch.sketchCurves.sketchLines.addTwoPointRectangle(
        adsk.core.Point3D.create(mm(x1), mm(y1), 0),
        adsk.core.Point3D.create(mm(x2), mm(y2), 0),
    )


def add_box_from_xy(comp, name, x1, y1, x2, y2, z_mm, height_mm, operation):
    plane = offset_plane(comp, comp.xYConstructionPlane, z_mm)
    sketch = comp.sketches.add(plane)
    add_rect_xy(sketch, x1, y1, x2, y2)
    feature = extrude(comp, sketch.profiles.item(0), height_mm, operation)
    for body in feature.bodies:
        body.name = name
    return feature


def add_rect_xz(sketch, x1, z1, x2, z2):
    return sketch.sketchCurves.sketchLines.addTwoPointRectangle(
        adsk.core.Point3D.create(mm(x1), mm(z1), 0),
        adsk.core.Point3D.create(mm(x2), mm(z2), 0),
    )


def add_box_from_xz(comp, name, x1, z1, x2, z2, y_mm, depth_mm, operation):
    plane = offset_plane(comp, comp.xZConstructionPlane, y_mm)
    sketch = comp.sketches.add(plane)
    add_rect_xz(sketch, x1, z1, x2, z2)
    feature = extrude(comp, sketch.profiles.item(0), depth_mm, operation)
    for body in feature.bodies:
        body.name = name
    return feature


def largest_profile(sketch):
    best = None
    best_area = -1
    for index in range(sketch.profiles.count):
        profile = sketch.profiles.item(index)
        bounds = profile.boundingBox
        area = (bounds.maxPoint.y - bounds.minPoint.y) * (bounds.maxPoint.z - bounds.minPoint.z)
        if area > best_area:
            best = profile
            best_area = area
    return best


def create_open_sleeve_profile(comp, inner_radius, wall_thickness, opening_angle_deg):
    """Create a closed C-shaped profile directly, avoiding boolean cuts."""
    outer_radius = inner_radius + wall_thickness
    half_gap = opening_angle_deg / 2.0
    start_deg = 90.0 + half_gap
    end_deg = 450.0 - half_gap
    steps = 72

    sketch = comp.sketches.add(comp.yZConstructionPlane)
    lines = sketch.sketchCurves.sketchLines
    points = []

    for step in range(steps + 1):
        angle = math.radians(start_deg + (end_deg - start_deg) * step / steps)
        points.append(point_yz(outer_radius * math.cos(angle), outer_radius * math.sin(angle)))

    for step in range(steps, -1, -1):
        angle = math.radians(start_deg + (end_deg - start_deg) * step / steps)
        points.append(point_yz(inner_radius * math.cos(angle), inner_radius * math.sin(angle)))

    for index in range(len(points)):
        lines.addByTwoPoints(points[index], points[(index + 1) % len(points)])

    return sketch


def build_model(comp, p):
    inner_r = p["cot_finger_diameter"] / 2.0
    wall = p["cot_wall_thickness"]
    sleeve_len = p["cot_length"]

    head_w = p["cot_sensor_head_width"]
    head_l = p["cot_sensor_head_length"]
    head_x1 = max(0.0, min(p["cot_sensor_from_tip"], sleeve_len - head_l))
    head_x2 = head_x1 + head_l
    head_clear_w = head_w + p["cot_sensor_clearance"]

    outer_r = inner_r + wall
    half_sensor_span = head_clear_w / 2.0
    max_adhesive_y = math.sqrt(
        max(0.0, (outer_r - p["cot_adhesive_pad_thickness"] - 0.05) ** 2 - half_sensor_span**2)
    )
    adhesive_y = min(inner_r - p["cot_sensor_preload"], max_adhesive_y)
    sensor_y = adhesive_y - p["cot_sensor_thickness"]

    sleeve_sketch = create_open_sleeve_profile(
        comp,
        inner_r,
        wall,
        p["cot_top_opening_angle"],
    )
    sleeve_feature = extrude(
        comp,
        largest_profile(sleeve_sketch),
        sleeve_len,
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    sleeve_body = sleeve_feature.bodies.item(0)
    sleeve_body.name = "print body - flexible fingertip cot"

    # Internal side-wall backing pad. The sensor adhesive sticks to this face;
    # the sensor contact side faces inward toward the fingertip. The y-location
    # is chord-limited so the flat 14 mm sensor pad stays inside the sleeve.
    pad_feature = add_box_from_xz(
        comp,
        "print body - internal sensor backing pad",
        head_x1 - p["cot_sensor_clearance"] / 2.0,
        -head_clear_w / 2.0,
        head_x2 + p["cot_sensor_clearance"] / 2.0,
        head_clear_w / 2.0,
        adhesive_y,
        p["cot_adhesive_pad_thickness"],
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )
    for body in pad_feature.bodies:
        body.name = "print body - internal sensor backing pad"

    # Very shallow tail guide line inside the sleeve. This is just enough surface
    # to tape the flexible tail without creating a bulky block.
    tail_pad_feature = add_box_from_xz(
        comp,
        "print body - thin internal tail tack strip",
        head_x2,
        -p["cot_sensor_tail_width"] / 2.0,
        sleeve_len,
        p["cot_sensor_tail_width"] / 2.0,
        adhesive_y,
        max(0.2, p["cot_adhesive_pad_thickness"] * 0.5),
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )
    for body in tail_pad_feature.bodies:
        body.name = "print body - thin internal tail tack strip"

    # Sensor references. Hide before exporting the printable body.
    sensor_feature = add_box_from_xz(
        comp,
        "reference only - pressure sensor head",
        head_x1,
        -head_w / 2.0,
        head_x2,
        head_w / 2.0,
        sensor_y,
        p["cot_sensor_thickness"],
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    sensor_feature.bodies.item(0).name = "reference only - pressure sensor head"

    tail_feature = add_box_from_xz(
        comp,
        "reference only - flexible sensor tail",
        head_x2,
        -p["cot_sensor_tail_width"] / 2.0,
        sleeve_len + 45.0,
        p["cot_sensor_tail_width"] / 2.0,
        sensor_y,
        p["cot_sensor_thickness"],
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    tail_feature.bodies.item(0).name = "reference only - flexible sensor tail"

    reinforcement_feature = add_box_from_xz(
        comp,
        "reference only - tail reinforcement",
        sleeve_len + 17.0,
        -p["cot_reinforcement_width"] / 2.0,
        sleeve_len + 45.0,
        p["cot_reinforcement_width"] / 2.0,
        adhesive_y,
        p["cot_sensor_thickness"],
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    reinforcement_feature.bodies.item(0).name = "reference only - tail reinforcement"

    cell_y = sensor_y - 0.08
    first_cell_x = head_x1 + (head_l - 2.0 * p["cot_cell_pitch"]) / 2.0
    first_cell_y = -p["cot_cell_pitch"]
    for row in range(3):
        for col in range(3):
            center_x = first_cell_x + col * p["cot_cell_pitch"]
            center_y = first_cell_y + row * p["cot_cell_pitch"]
            cell_feature = add_box_from_xz(
                comp,
                "reference only - pressure cell",
                center_x - p["cot_cell_size"] / 2.0,
                center_y - p["cot_cell_size"] / 2.0,
                center_x + p["cot_cell_size"] / 2.0,
                center_y + p["cot_cell_size"] / 2.0,
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
            "Created elastic fingertip pressure sensor cot.\n\n"
            "Print the wearable body in TPU or another flexible material.\n"
            "Hide all 'reference only' bodies before exporting STL/3MF.\n"
            "Tune cot_finger_diameter, cot_top_opening_angle, and cot_sensor_preload first."
        )
    except Exception:
        if ui:
            ui.messageBox("Script failed:\n{}".format(traceback.format_exc()))


def stop(context):
    pass
