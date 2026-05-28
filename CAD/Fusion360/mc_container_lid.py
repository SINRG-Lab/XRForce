"""Fusion 360 script: parametric lid for MC_Container.

Run from Fusion 360: Utilities > Scripts and Add-Ins > Scripts > + > select
this file.

The script creates a replacement component named "MC Container Lid" with
editable user parameters. Measure the container mouth, then tune the
mc_lid_* parameters in Modify > Change Parameters and rerun the script.
"""

import math
import traceback

import adsk.core
import adsk.fusion


COMPONENT_NAME = "MC Container Lid"


DEFAULTS_MM = {
    # Top plate footprint. Set these slightly larger than the outside rim.
    "mc_lid_top_length": (82.0, "Overall lid top plate length"),
    "mc_lid_top_width": (52.0, "Overall lid top plate width"),
    "mc_lid_top_thickness": (2.4, "Lid top plate thickness"),
    "mc_lid_top_corner_radius": (5.0, "Top plate corner radius"),
    "mc_lid_edge_fillet": (0.6, "Small exterior edge softening radius"),
    # Underside locating skirt. Set skirt outer size to container inside opening
    # minus about 0.3-0.6 mm total, depending on printer/material.
    "mc_lid_skirt_outer_length": (76.0, "Outer length of underside locating skirt"),
    "mc_lid_skirt_outer_width": (46.0, "Outer width of underside locating skirt"),
    "mc_lid_skirt_wall": (1.6, "Wall thickness of underside locating skirt"),
    "mc_lid_skirt_depth": (6.0, "Depth of underside skirt into the container"),
    # Handle.
    "mc_lid_handle_length": (36.0, "Pull handle length"),
    "mc_lid_handle_width": (10.0, "Pull handle width"),
    "mc_lid_handle_height": (3.2, "Pull handle height above lid"),
    "mc_lid_handle_corner_radius": (3.0, "Pull handle corner radius"),
}


def mm(value):
    """Fusion API length values are centimeters."""
    return value / 10.0


def point_xy(x_mm, y_mm):
    return adsk.core.Point3D.create(mm(x_mm), mm(y_mm), 0)


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


def offset_plane(comp, base_plane, offset_mm):
    planes = comp.constructionPlanes
    plane_input = planes.createInput()
    plane_input.setByOffset(base_plane, adsk.core.ValueInput.createByReal(mm(offset_mm)))
    return planes.add(plane_input)


def extrude(comp, profile, distance_mm, operation):
    extrudes = comp.features.extrudeFeatures
    ext_input = extrudes.createInput(profile, operation)
    ext_input.setDistanceExtent(False, adsk.core.ValueInput.createByReal(mm(distance_mm)))
    return extrudes.add(ext_input)


def add_rect(sketch, x1, y1, x2, y2):
    return sketch.sketchCurves.sketchLines.addTwoPointRectangle(
        point_xy(x1, y1),
        point_xy(x2, y2),
    )


def add_rounded_rect(sketch, length_mm, width_mm, radius_mm):
    radius = max(0.0, min(radius_mm, length_mm / 2.0 - 0.01, width_mm / 2.0 - 0.01))
    half_l = length_mm / 2.0
    half_w = width_mm / 2.0
    lines = sketch.sketchCurves.sketchLines
    arcs = sketch.sketchCurves.sketchArcs

    if radius <= 0.0:
        return add_rect(sketch, -half_l, -half_w, half_l, half_w)

    # Draw the perimeter counter-clockwise from the top-right straight segment.
    lines.addByTwoPoints(point_xy(half_l - radius, half_w), point_xy(-half_l + radius, half_w))
    arcs.addByCenterStartSweep(
        point_xy(-half_l + radius, half_w - radius),
        point_xy(-half_l + radius, half_w),
        math.pi / 2.0,
    )
    lines.addByTwoPoints(point_xy(-half_l, half_w - radius), point_xy(-half_l, -half_w + radius))
    arcs.addByCenterStartSweep(
        point_xy(-half_l + radius, -half_w + radius),
        point_xy(-half_l, -half_w + radius),
        math.pi / 2.0,
    )
    lines.addByTwoPoints(point_xy(-half_l + radius, -half_w), point_xy(half_l - radius, -half_w))
    arcs.addByCenterStartSweep(
        point_xy(half_l - radius, -half_w + radius),
        point_xy(half_l - radius, -half_w),
        math.pi / 2.0,
    )
    lines.addByTwoPoints(point_xy(half_l, -half_w + radius), point_xy(half_l, half_w - radius))
    arcs.addByCenterStartSweep(
        point_xy(half_l - radius, half_w - radius),
        point_xy(half_l, half_w - radius),
        math.pi / 2.0,
    )


def largest_profile(sketch):
    best = None
    best_area = -1.0
    for index in range(sketch.profiles.count):
        profile = sketch.profiles.item(index)
        bounds = profile.boundingBox
        area = (bounds.maxPoint.x - bounds.minPoint.x) * (bounds.maxPoint.y - bounds.minPoint.y)
        if area > best_area:
            best = profile
            best_area = area
    return best


def add_box_from_xy(comp, name, x1, y1, x2, y2, z_mm, height_mm, operation):
    plane = offset_plane(comp, comp.xYConstructionPlane, z_mm)
    sketch = comp.sketches.add(plane)
    add_rect(sketch, x1, y1, x2, y2)
    feature = extrude(comp, sketch.profiles.item(0), height_mm, operation)
    for body in feature.bodies:
        body.name = name
    return feature


def add_rounded_box_from_xy(comp, name, length, width, radius, z_mm, height_mm, operation):
    plane = offset_plane(comp, comp.xYConstructionPlane, z_mm)
    sketch = comp.sketches.add(plane)
    add_rounded_rect(sketch, length, width, radius)
    feature = extrude(comp, largest_profile(sketch), height_mm, operation)
    for body in feature.bodies:
        body.name = name
    return feature


def try_fillet_body(comp, body, radius_mm):
    if radius_mm <= 0.0:
        return

    edges = adsk.core.ObjectCollection.create()
    for index in range(body.edges.count):
        edges.add(body.edges.item(index))

    if edges.count == 0:
        return

    fillets = comp.features.filletFeatures
    fillet_input = fillets.createInput()
    fillet_input.addConstantRadiusEdgeSet(
        edges,
        adsk.core.ValueInput.createByReal(mm(radius_mm)),
        True,
    )
    try:
        fillets.add(fillet_input)
    except Exception:
        # If a user parameter makes the radius too large for one edge, keep the
        # printable lid geometry instead of failing the whole generator.
        pass


def build_model(comp, p):
    top_length = p["mc_lid_top_length"]
    top_width = p["mc_lid_top_width"]
    top_thickness = p["mc_lid_top_thickness"]
    skirt_outer_l = p["mc_lid_skirt_outer_length"]
    skirt_outer_w = p["mc_lid_skirt_outer_width"]
    skirt_wall = p["mc_lid_skirt_wall"]
    skirt_depth = p["mc_lid_skirt_depth"]

    min_inner_l = max(0.0, skirt_outer_l - 2.0 * skirt_wall)
    min_inner_w = max(0.0, skirt_outer_w - 2.0 * skirt_wall)

    top_feature = add_rounded_box_from_xy(
        comp,
        "print body - lid top plate",
        top_length,
        top_width,
        p["mc_lid_top_corner_radius"],
        0.0,
        top_thickness,
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    lid_body = top_feature.bodies.item(0)
    lid_body.name = "print body - MC container lid"

    # Four underside skirt walls. The skirt centers itself in the container mouth
    # while leaving the middle open.
    half_outer_l = skirt_outer_l / 2.0
    half_outer_w = skirt_outer_w / 2.0
    half_inner_l = min_inner_l / 2.0
    half_inner_w = min_inner_w / 2.0
    skirt_z = -skirt_depth

    add_box_from_xy(
        comp,
        "print body - front skirt wall",
        -half_outer_l,
        half_inner_w,
        half_outer_l,
        half_outer_w,
        0.0,
        -skirt_depth,
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )
    add_box_from_xy(
        comp,
        "print body - rear skirt wall",
        -half_outer_l,
        -half_outer_w,
        half_outer_l,
        -half_inner_w,
        0.0,
        -skirt_depth,
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )
    add_box_from_xy(
        comp,
        "print body - left skirt wall",
        -half_outer_l,
        -half_inner_w,
        -half_inner_l,
        half_inner_w,
        0.0,
        -skirt_depth,
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )
    add_box_from_xy(
        comp,
        "print body - right skirt wall",
        half_inner_l,
        -half_inner_w,
        half_outer_l,
        half_inner_w,
        0.0,
        -skirt_depth,
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )

    handle_feature = add_rounded_box_from_xy(
        comp,
        "print body - low pull handle",
        p["mc_lid_handle_length"],
        p["mc_lid_handle_width"],
        p["mc_lid_handle_corner_radius"],
        top_thickness,
        p["mc_lid_handle_height"],
        adsk.fusion.FeatureOperations.JoinFeatureOperation,
    )
    for body in handle_feature.bodies:
        body.name = "print body - MC container lid"

    try_fillet_body(comp, lid_body, p["mc_lid_edge_fillet"])

    # A reference outline showing the nominal container inside opening that the
    # skirt is designed to slip into.
    ref_feature = add_rounded_box_from_xy(
        comp,
        "reference only - container opening clearance envelope",
        skirt_outer_l,
        skirt_outer_w,
        max(0.0, p["mc_lid_top_corner_radius"] - (top_length - skirt_outer_l) / 2.0),
        skirt_z - 0.3,
        0.3,
        adsk.fusion.FeatureOperations.NewBodyFeatureOperation,
    )
    ref_feature.bodies.item(0).name = "reference only - skirt outer fit envelope"


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
            "Created MC Container Lid.\n\n"
            "Measure the MC_Container mouth and tune mc_lid_skirt_outer_length, "
            "mc_lid_skirt_outer_width, and mc_lid_skirt_wall first.\n"
            "Hide the 'reference only' body before exporting STL/3MF."
        )
    except Exception:
        if ui:
            ui.messageBox("Script failed:\n{}".format(traceback.format_exc()))


def stop(context):
    pass
