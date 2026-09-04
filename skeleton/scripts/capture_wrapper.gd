extends Node3D
## M07 capture wrapper (bernie, 2026-09-04)
##
## Wraps a zone scene so we can snapshot it headlessly and prove the render is
## real and DISTINCT per zone. The zone scene to load is taken from the
## ZONE_SCENE env var (a res:// path). We instance it, await a few frames so
## the static terrain mesh + sky + lights draw, then write the viewport to
## OUT_PATH and quit 0.
##
## Run (Forward+ needs a GL context, so wrap in xvfb-run):
##   ZONE_SCENE=res://zones/meadow/meadow.tscn OUT_PATH=/abs/meadow.png \
##     xvfb-run -a godot --path <proj> --editor scenery ...
## Or run the scene directly:
##   godot --path <proj> res://scripts/capture_scene.tscn

func _ready() -> void:
    var zone_path: String = OS.get_environment("ZONE_SCENE")
    var out: String = OS.get_environment("OUT_PATH")
    if zone_path.is_empty():
        push_error("[capture] ZONE_SCENE unset")
        get_tree().quit(2)
        return
    if out.is_empty():
        push_error("[capture] OUT_PATH unset")
        get_tree().quit(2)
        return
    var packed: PackedScene = load(zone_path)
    if packed == null:
        push_error("[capture] cannot load zone %s" % zone_path)
        get_tree().quit(3)
        return
    var inst: Node = packed.instantiate()
    add_child(inst)
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().process_frame
    var img := get_viewport().get_texture().get_image()
    var err := img.save_png(out)
    print("[capture] saved %s (%dx%d) err=%d" % [out, img.get_width(), img.get_height(), err])
    get_tree().quit(0)
