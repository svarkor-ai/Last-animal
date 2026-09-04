extends Node3D
## M07 zone root script (Last Animal world-environment)
##
## Responsibilities per the M07 DoD / PHASE0.md Phase 6:
##   - exposes the M12 spawner hook key `zone_id` (C15: EcosystemSpawner.OnZoneEnter)
##     on a named `Spawners` node so later modules attach spawn logic there
##   - bakes the NavigationRegion3D navmesh at runtime from the SAME terrain mesh the
##     visual ground uses, so enemies/companions (M08/M05) can pathfind the zone.
##     Navmesh bake is fully headless (NavigationServer3D, Godot 4.7's documented path).

@export var zone_id: StringName = &"zone"

@onready var ground: Node3D = $Ground

func _ready() -> void:
    # M12 hook surface: record the zone id on the Spawners node so
    # EcosystemSpawner.OnZoneEnter(zoneId) (C15) has a stable, queryable anchor.
    spawners().set_meta("zone_id", String(zone_id))
    print("[zone] %s ready; zone_id=%s" % [name, zone_id])
    bake_navmesh.call_deferred()

func spawners() -> Node3D:
    return get_node_or_null("Spawners") if has_node("Spawners") else self

func bake_navmesh() -> void:
    var nav := get_node_or_null("Navigation") as NavigationRegion3D
    if nav == null:
        push_warning("[zone] %s: no Navigation region to bake" % name)
        return
    # Source geometry for the navmesh = a CPU-side triangle mesh built from the SAME
    # height data as the ground's collision + visual mesh. HeightMapShape3D collision is
    # not directly navmesh-bakeable, so we feed this matching heightfield mesh instead —
    # walkable poly coincides with the ground the player stands on, with no GPU read-back.
    var src := NavigationMeshSourceGeometryData3D.new()
    var src_mesh: ArrayMesh = null
    if ground.has_method("build_heightfield_mesh"):
        src_mesh = ground.build_heightfield_mesh()
    if src_mesh != null:
        src.add_mesh(src_mesh, Transform3D.IDENTITY)
    NavigationServer3D.bake_from_source_geometry_data(nav.navigation_mesh, src)
    var poly: int = nav.navigation_mesh.get_polygon_count()
    var ok: bool = poly > 0
    if ok:
        print("[zone] %s: navmesh baked, polygons=%d" % [name, poly])
    else:
        push_warning("[zone] %s: navmesh bake produced 0 polygons (terrain too steep?)" % name)
