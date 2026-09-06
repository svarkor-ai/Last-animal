extends Node3D
## M08 combat-3d Forward+ crowd-scene render proof (MC 890.13, artemis, 2026-09-06)
##
## Phase-10 DoD (PHASE0.md Phase 10): "Forward+ crowd scene renders non-blank
## (graphical-test-helper)". This scene builds a crowd of enemy-ish meshes
## around the player on a ground plane under Forward+ (project.godot is
## already forward_plus), lights, and an isometric-style camera — then, after
## a few frames so the meshes + lights + sky actually draw, it writes the
## viewport to OUT_PATH and quits 0.
##
## The "crowd" is placed around the player like a PoE-style engagement: a ring
## of enemies inside/at chase range plus a visible player capsule, on a flat
## arena ground. Geometrically identical meshes auto-instance under Forward+;
## colors vary via per-mesh material so the render is visibly a crowd, not a
## single cube.
##
## Run (Forward+ needs a GL context — wrap in xvfb-run):
##   OUT_PATH=/abs/crowd.png xvfb-run -a godot --path <proj> \
##     res://scenes/combat/crowd_scene.tscn

const N_ENEMIES := 24          # ring size around the player = a "crowd"
const ENEMY_Y := 0.55          # enemy mesh half-height so it sits above ground

func _ready() -> void:
    _build_arena()
    _build_lighting()
    _build_player()
    _build_crowd()
    _build_camera()
    _snapshot_and_quit()

# --- scene construction -----------------------------------------------------

func _build_arena() -> void:
    # Ground plane (arena floor). A simple thick box reads clearly on the
    # Forward+ render and gives the crowd something to stand on.
    var floor := MeshInstance3D.new()
    var box := BoxMesh.new()
    box.size = Vector3(40.0, 0.5, 40.0)
    var mat := StandardMaterial3D.new()
    mat.albedo_color = Color(0.25, 0.30, 0.22)   # dark meadow green
    box.material = mat
    floor.mesh = box
    floor.position = Vector3(0.0, -0.25, 0.0)
    add_child(floor)

func _build_lighting() -> void:
    var dir := DirectionalLight3D.new()
    dir.rotation_degrees = Vector3(-55.0, 30.0, 0.0)
    dir.light_energy = 1.1
    add_child(dir)
    var hemi := DirectionalLight3D.new()
    hemi.rotation_degrees = Vector3(35.0, -40.0, 0.0)
    hemi.light_energy = 0.35
    hemi.light_color = Color(0.6, 0.7, 1.0)
    add_child(hemi)

func _build_player() -> void:
    # Player as a warm grey capsule (the controllable character the HUD binds).
    var p := MeshInstance3D.new()
    var cap := CapsuleMesh.new()
    cap.radius = 0.35
    cap.height = 1.8
    var mat := StandardMaterial3D.new()
    mat.albedo_color = Color(0.85, 0.82, 0.75)
    p.mesh = cap
    p.position = Vector3(0.0, 0.9, 0.0)
    add_child(p)

func _build_crowd() -> void:
    # A ring of enemies around the player at varied radii + a couple closer.
    # Each gets its own material color so they read as distinct enemies; the
    # meshes are all BoxMesh (auto-instanced by Forward+).
    var radius := 2.5
    var box := BoxMesh.new()
    box.size = Vector3(0.6, 1.2, 0.6)
    var palette := [
        Color(0.62, 0.28, 0.20),  # goblin-ish
        Color(0.72, 0.48, 0.22),  # orc-ish
        Color(0.80, 0.80, 0.82),  # skeleton-ish
        Color(0.70, 0.12, 0.12),  # demon-ish
    ]
    for i in N_ENEMIES:
        var m := MeshInstance3D.new()
        m.mesh = box
        var mat := StandardMaterial3D.new()
        mat.albedo_color = palette[i % palette.size()]
        m.material_override = mat
        var angle := TAU * float(i) / float(N_ENEMIES)
        var dist := radius + (0.4 * float(i % 4))   # ragged ring = crowd
        m.position = Vector3(cos(angle) * dist, ENEMY_Y, sin(angle) * dist)
        add_child(m)

func _build_camera() -> void:
    # Isometric-ish follow camera (phase 0 C10: "isometric camera"): above and
    # slightly back, looking at the arena centre where the crowd is.
    var cam := Camera3D.new()
    cam.position = Vector3(0.0, 8.0, 9.0)
    cam.rotation_degrees = Vector3(-40.0, 0.0, 0.0)
    add_child(cam)

# --- capture + quit ---------------------------------------------------------

func _snapshot_and_quit() -> void:
    var out: String = OS.get_environment("OUT_PATH")
    if out.is_empty():
        push_error("[crowd] OUT_PATH unset")
        get_tree().quit(2)
        return
    # Let several frames draw so meshes + lights + sky are in the buffer.
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().process_frame
    await get_tree().process_frame
    # Optional hold so the graphical-test-helper harness can capture the live
    # window (it snapshots the display after a settle delay). Defaults 0.
    var hold_ms: int = int(OS.get_environment("HOLD_MS")) if OS.get_environment("HOLD_MS") != "" else 0
    if hold_ms > 0:
        await get_tree().create_timer(float(hold_ms) / 1000.0).timeout
    var img := get_viewport().get_texture().get_image()
    var err := img.save_png(out)
    print("[crowd] saved %s (%dx%d) err=%d" % [out, img.get_width(), img.get_height(), err])
    get_tree().quit(0 if err == OK else 3)
