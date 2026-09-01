# Road Network And Building Access

Date: 2026-09-01
Audience: Codex agents only
Status: design reasoning, not implementation permission

## Session Context

The user paused implementation to reason about how a Cities: Skylines-like simulation should represent roads, buildings, and pathfinding. The user wants to learn by thinking, writing, making mistakes, and iterating. Do not convert this note directly into code unless the user explicitly asks.

## User Observations

- Buildings should use world coordinates and should not be forced onto road-grid cells.
- Some Cities: Skylines 1 mods allow buildings to be placed outside normal road/zoning constraints, suggesting placement freedom and road access can be separate concerns.
- Roads appear to have nodes and segments; even a straight road may be represented by multiple shorter segments.
- The visible road is not the same as the pathfinding structure. The road network mainly serves navigation, service reachability, traffic, and simulation.
- If every building became a permanent pathfinding node, the graph could grow with building count faster than the road network grows.
- If pathfinding only targets road nodes, the final approach to a building could look too simple or unnatural.
- The user proposed solving the "last 100 meters" with deterministic local logic after global road pathfinding reaches the target-side road area.

## Current Working Hypothesis

Separate the simulation into at least three layers:

1. Road graph layer: stable nodes and segments used for global routing.
2. Building access layer: building entrances attach to road segments without forcing every building into the permanent road graph.
3. Local approach layer: after global routing, movement follows the target segment to the attachment point, then follows the local connection into the building.

This preserves detail without making the global routing graph scale directly with building count.

## Terms To Preserve

- `Building.worldPosition`: the building's actual position in world space.
- `BuildingEntrance`: a point on or near the building where agents enter or exit.
- `RoadNode`: a stable graph node, usually road endpoints or intersections.
- `RoadSegment`: a connection between road nodes; may have geometry more detailed than a straight line.
- `RoadAttachment`: a cached relationship from a building entrance to a specific road segment.
- `t`: a normalized position along a road segment, where 0 is the start node and 1 is the end node.
- `Last 100 meters`: local approach logic from the road graph to the building entrance.

## Reasoning To Revisit

- Circle/radius detection is useful as a broad-phase query: find candidate roads near a building entrance.
- Precise connection should use nearest point on road segment or lane, not just circle intersection.
- Road segments should not be split for every building by default; permanent splitting may create too many graph nodes.
- A building attachment can store segment plus `t`; local logic can compute distance from either segment end to the attachment point.
- Global routing can ignore most buildings most of the time; only origin and destination buildings need temporary/local access handling during a route query.

## External Evidence Anchors

- Traffic Manager: President Edition documentation describes Cities: Skylines networks in terms of nodes, segments, and lanes: https://doc.tmpe.me/nodes-segments-lanes.html
- Colossal Order's Cities: Skylines II traffic AI diary contrasts older proximity-based service/path decisions with more route-cost-aware behavior: https://colossalorder.fi/news/development-diary-2-traffic-ai/
- OSRM exposes `nearest` to snap coordinates to the street network, a useful analogy for building entrance to road attachment: https://project-osrm.org/docs/v5.5.1/api/#nearest-service
- GraphHopper includes map matching, another example of matching free coordinates or traces to road-network geometry: https://github.com/graphhopper/graphhopper/tree/master/map-matching
- Valhalla documents tiled hierarchical routing data, relevant later when the project needs large-city scaling: https://valhalla.github.io/valhalla/

## Teaching Guidance

Good next prompt:

> Explain, in your own words, the difference between building position, building entrance, road segment, and road attachment.

Good first exercise, if the user asks to write code:

> Given one road segment from A to B and one building entrance point, calculate the nearest point on the segment and the attachment `t`.

Do not start with A*, server sync, traffic agents, zoning growth, or full city simulation. The educational target is to make one relationship clear enough that the user can explain it.
