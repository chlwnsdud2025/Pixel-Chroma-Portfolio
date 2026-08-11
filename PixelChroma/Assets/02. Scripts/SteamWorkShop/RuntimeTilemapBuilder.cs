using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuntimeTilemapBuilder
{
    public static GameObject Build(PixelChromaMapExportData mapData, Vector2 offset)
    {
        // 1. 루트 오브젝트
        var root = new GameObject(mapData.runtime?.mapRootObjectName ?? "WorkshopMap");
        root.transform.position = new Vector3(offset.x, offset.y, 0f);

        // 2. Grid
        var gridGo = new GameObject(mapData.runtime?.gridObjectName ?? "Grid");
        gridGo.transform.SetParent(root.transform, false);
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        // 3. layerBindings에서 Tilemap 생성
        var tilemaps = new System.Collections.Generic.Dictionary<string, Tilemap>();

        if (mapData.runtime?.layerBindings != null)
        {
            foreach (var binding in mapData.runtime.layerBindings)
            {
                if (binding.metadataOnly || string.IsNullOrEmpty(binding.targetTilemapName))
                    continue;

                if (tilemaps.ContainsKey(binding.targetTilemapName))
                    continue;

                var layerGo = new GameObject(binding.targetTilemapName);
                layerGo.transform.SetParent(gridGo.transform, false);

                // Unity Physics Layer 설정
                if (!string.IsNullOrEmpty(binding.unityLayerName))
                {
                    int layer = LayerMask.NameToLayer(binding.unityLayerName);
                    if (layer >= 0)
                        layerGo.layer = layer;
                }

                var tilemap = layerGo.AddComponent<Tilemap>();

                if (binding.createsRenderer)
                {
                    var renderer = layerGo.AddComponent<TilemapRenderer>();
                    renderer.sortingOrder = binding.sortingOrder;
                }

                if (binding.createsCollider)
                {
                    var rb = layerGo.AddComponent<Rigidbody2D>();
                    rb.bodyType = RigidbodyType2D.Static;

                    layerGo.AddComponent<TilemapCollider2D>();

                    var composite = layerGo.AddComponent<CompositeCollider2D>();
                    composite.geometryType = CompositeCollider2D.GeometryType.Outlines;

                    var tilemapCollider = layerGo.GetComponent<TilemapCollider2D>();
                    tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
                }

                tilemaps[binding.targetTilemapName] = tilemap;
            }
        }

        // 4. 레이어별 타일 배치
        if (mapData.layers != null)
        {
            foreach (var layer in mapData.layers)
            {
                if (layer.tiles == null || layer.tiles.Count == 0)
                    continue;

                // 레이어 이름으로 tilemap 찾기
                Tilemap targetTilemap = null;
                if (mapData.runtime?.layerBindings != null)
                {
                    foreach (var binding in mapData.runtime.layerBindings)
                    {
                        if (binding.exportLayerName == layer.name
                            && !binding.metadataOnly
                            && !string.IsNullOrEmpty(binding.targetTilemapName))
                        {
                            tilemaps.TryGetValue(binding.targetTilemapName, out targetTilemap);
                            break;
                        }
                    }
                }

                if (targetTilemap == null)
                    continue;

                foreach (var tile in layer.tiles)
                {
                    TileBase unityTile = RuntimeTileFactory.CreateTile(tile);
                    if (unityTile == null)
                        continue;

                    // 좌표 변환: unityY = height - 1 - mapY
                    int unityY = mapData.height - 1 - tile.y;
                    targetTilemap.SetTile(new Vector3Int(tile.x, unityY, 0), unityTile);
                }
            }
        }

        // 5. 스폰 포인트 생성
        if (mapData.spawnPoints != null)
        {
            foreach (var sp in mapData.spawnPoints)
            {
                var spawnGo = new GameObject(string.IsNullOrEmpty(sp.id) ? "SpawnPoint" : sp.id);
                spawnGo.transform.SetParent(root.transform, false);
                spawnGo.transform.localPosition = new Vector3(sp.worldX, sp.worldY, 0f);
                spawnGo.AddComponent<PixelChroma.MapSpawnPoint>();
            }
        }

        // 6. 존 생성
        if (mapData.zones != null)
        {
            foreach (var zone in mapData.zones)
            {
                var zoneGo = new GameObject(string.IsNullOrEmpty(zone.id) ? "Zone" : zone.id);
                zoneGo.transform.SetParent(root.transform, false);

                // 존의 좌표도 Y 변환 필요
                float zoneUnityY = mapData.height - 1 - zone.y - (zone.height - 1) * 0.5f;
                float zoneCenterX = zone.x + (zone.width - 1) * 0.5f + 0.5f;
                float zoneCenterY = zoneUnityY + 0.5f;
                zoneGo.transform.localPosition = new Vector3(zoneCenterX, zoneCenterY, 0f);

                if (zone.collider)
                {
                    var box = zoneGo.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.size = new Vector2(zone.width, zone.height);
                }
            }
        }

        return root;
    }
}