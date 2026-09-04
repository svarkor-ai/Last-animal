extends Node
## M07 distinct-render capture helper (bernie, 2026-09-04)
##
## Runs as an autoload-style addition: it waits for the current frame to be
## drawn, grabs the root viewport texture and writes a PNG. Launch with
## `godot --path <proj> --script <this> ` won't attach to a scene; instead we
## add this as a child of the scene root via a wrapper scene. See capture_main.gd.
##
## Usage (wrapper scene script): it snapshots the viewport N frames after
## entering tree and quits. Env OUT_PATH controls where the PNG lands.
extends Node

func _ready() -> void:
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().process_frame
    var vp := get_viewport()
    var img := vp.get_texture().get_image()
    var out: String = OS.get_environment("OUT_PATH")
    if out.is_empty():
        out = "res://capture.png"
    img.save_png(out)
    print("[capture] saved %s (%dx%d)" % [out, img.get_width(), img.get_height()])
    get_tree().quit(0)
