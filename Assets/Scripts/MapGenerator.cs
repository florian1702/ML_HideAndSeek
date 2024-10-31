using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class MapGenerator : MonoBehaviour
{
    [SerializeField] private float mapSize = 20f; // controls where objects are randomized
    [SerializeField] private GameObject wallPrefab = null;
    [SerializeField] private Transform wallsParent = null;
    [SerializeField] private float wallY = 1f;
    [SerializeField] private float wallThickness = 0.25f;
    [SerializeField] private float wallsPosition = 0f; // if equal to 0, then mapSize value is used instead
    [SerializeField] private GameObject floorGameObject = null;

    [Header("Agent placement")]
    // instantiateAgents must be on if agent count should be randomized every episode
    [SerializeField] private Transform agentParent = null;
    [SerializeField] private  HiderAgent hiderPrefab = null;
    [SerializeField] private SeekerAgent seekerPrefab = null;
    [SerializeField] private int numHidersMin = 3;
    [SerializeField] private int numHidersMax = 3;
    [SerializeField] private int numSeekersMin = 3;
    [SerializeField] private int numSeekersMax = 3;
    [SerializeField] private float agentY = 1f;
    [SerializeField] private float agentRadius = 0.75f;

    [Header("Object placement")]
    // instantiateBoxes must be on if box count should be randomized every episode
    [SerializeField] private bool instantiateInteractables = true;
    [SerializeField] private Box[] boxPrefabs = null;
    [SerializeField] private Ramp[] rampPrefabs = null;
    [SerializeField] private int numBoxesMin = 2;
    [SerializeField] private int numBoxesMax = 4;
    [SerializeField] private int numRampsMin = 2;
    [SerializeField] private int numRampsMax = 4;
    [SerializeField] private float boxY = 2f;
    [SerializeField] private float rampY = 2f;
    [SerializeField] private float objectRadius = 2.5f;

    [Header("Subroom generation")]
    [SerializeField] private bool generateSubroom = false;
    [SerializeField] private float roomSize = 10f;
    [SerializeField] private float doorWidth = 2.5f;

    private const int numTriesAgent = 50;
    private const int numTriesBox = 30;
    private const int numTriesRamp = 30;

    private List<GameObject> generatedWalls = null;

    private HiderAgent[] hiders = null;
    private SeekerAgent[] seekers = null;
    private Box[] boxes = null;
    private Ramp[] ramps = null;

    public int NumHiders { get; private set; } = 0;
    public int NumSeekers { get; private set; } = 0;
    public List<HiderAgent> GetInstantiatedHiders() => hiders.ToList();
    public List<SeekerAgent> GetInstantiatedSeekers() => seekers.ToList();

    public void Initialize()
    {
        hiders = Enumerable.Range(0, numHidersMax).Select(_ => Instantiate(hiderPrefab, agentParent)).ToArray();
        seekers = Enumerable.Range(0, numSeekersMax).Select(_ => Instantiate(seekerPrefab, agentParent)).ToArray();

        if (!instantiateInteractables)
        {
            boxes = FindObjectsByType<Box>(0);
            ramps = FindObjectsByType<Ramp>(0);
        }
    }

    public void Generate()
    {
        generatedWalls?.ForEach((GameObject wall) => Destroy(wall));
        generatedWalls = new List<GameObject>();
        GenerateMainRoom();
        
        if (instantiateInteractables)
        {
            if (boxes != null)
            {
                Array.ForEach(boxes, (Box box) => Destroy(box.gameObject));
            }
            if (ramps != null)
            {
                Array.ForEach(ramps, (Ramp ramp) => Destroy(ramp.gameObject));
            }
        }

        if (generateSubroom)
        {
            PlaceSubroomWalls(0f);
            PlaceSubroomWalls(-90f);
        }

        PlaceStuff();
    }

    private void GenerateMainRoom()
    {
        float size = wallsPosition == 0f ? mapSize : wallsPosition;

        floorGameObject.transform.localScale = new Vector3(size, size, 1);
        float sx = size;
        float sz = wallThickness;
        Vector3 pos = new Vector3(0f, wallY, size * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            GameObject wall = Instantiate(wallPrefab, pos + transform.position, Quaternion.identity, wallsParent);
            wall.transform.localScale = new Vector3(sx, wall.transform.localScale.y, sz);
            generatedWalls.Add(wall);

            float tmp = sx;
            sx = sz;
            sz = tmp;

            pos = Quaternion.AngleAxis(90f, Vector3.up) * pos;
        }
    }

    private void PlaceSubroomWalls(float rotation)
    {
        Vector3 roomCenter = 0.5f * (mapSize - roomSize) * new Vector3(1f, 0f, -1f);
        float wallZ = -mapSize * 0.5f + roomSize;

        float doorPosition = Random.Range(doorWidth * 0.5f, roomSize - doorWidth * 0.5f);
        if (doorPosition > doorWidth * 0.5f + 0.25f)
        {
            float wallLength = doorPosition - doorWidth * 0.5f;
            float wallX = mapSize * 0.5f - roomSize + wallLength * 0.5f;
            GameObject wall = Instantiate(wallPrefab, new Vector3(wallX, wallY, wallZ) + transform.position, Quaternion.identity, wallsParent);
            wall.transform.localScale = new Vector3(wallLength, wall.transform.localScale.y, wall.transform.localScale.z);
            wall.transform.RotateAround(roomCenter, Vector3.up, rotation);

            generatedWalls.Add(wall);
        }
        if (doorPosition < roomSize - doorWidth * 0.5f - 0.25f)
        {
            float wallLength = roomSize - doorPosition - doorWidth * 0.5f;
            float wallX = mapSize * 0.5f - wallLength * 0.5f;
            GameObject wall = Instantiate(wallPrefab, new Vector3(wallX, wallY, wallZ) + transform.position, Quaternion.identity, wallsParent);
            wall.transform.localScale = new Vector3(wallLength, wall.transform.localScale.y, wall.transform.localScale.z);
            wall.transform.RotateAround(roomCenter, Vector3.up, rotation);

            generatedWalls.Add(wall);
        }
    }

    private void PlaceStuff()
    {
        List<(Vector2, float)> itemPlacement = new List<(Vector2, float)>();

        NumHiders = Random.Range(numHidersMin, numHidersMax + 1);
        NumSeekers = Random.Range(numSeekersMin, numSeekersMax + 1);
        for (int i = 0; i < NumHiders; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointHider, agentRadius, numTriesAgent))
            {
                // this shouldn't happen during the training, as it may break trainer
                Debug.LogError("Couldn't randomize agent placement");
                return;
            }
        }
        for (int i = 0; i < NumSeekers; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointSeeker, agentRadius, numTriesAgent))
            {
                // this shouldn't happen during the training, as it may break trainer
                Debug.LogError("Couldn't randomize agent placement");
                return;
            }
        }

        int numBoxes = instantiateInteractables ? Random.Range(numBoxesMin, numBoxesMax + 1) : boxes.Length;
        for (int i = 0; i < numBoxes; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointBox, objectRadius, numTriesBox))
            {
                Debug.LogWarning("Couldn't randomize box placement");
                numBoxes = i;
                break;
            }
        }

        int numRamps = instantiateInteractables ? Random.Range(numRampsMin, numRampsMax + 1) : ramps.Length;
        for (int i = 0; i < numRamps; i++)
        {
            if (!TryPlaceObject(itemPlacement, PickPointRamp, objectRadius, numTriesRamp))
            {
                Debug.LogWarning("Couldn't randomize ramp placement");
                numRamps = i;
                break;
            }
        }

        
        for (int i = 0; i < NumHiders; i++)
        {
            float x = itemPlacement[i].Item1.x;
            float z = itemPlacement[i].Item1.y;
            hiders[i].transform.position = new Vector3(x, agentY, z) + transform.position;
            hiders[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        for (int i = 0; i < NumSeekers; i++)
        {
            int id = i + NumHiders;
            float x = itemPlacement[id].Item1.x;
            float z = itemPlacement[id].Item1.y;
            seekers[i].transform.position = new Vector3(x, agentY, z) + transform.position;
            seekers[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        if (instantiateInteractables)
        {
            boxes = new Box[numBoxes];
            ramps = new Ramp[numRamps];
        }
        for (int i = 0; i < numBoxes; i++)
        {
            int id = i + NumHiders + NumSeekers;
            float x = itemPlacement[id].Item1.x;
            float z = itemPlacement[id].Item1.y;
            if (instantiateInteractables)
            {
                Box boxPrefab = boxPrefabs[Random.Range(0, boxPrefabs.Length)];
                boxes[i] = Instantiate(boxPrefab);
            }
            boxes[i].transform.position = new Vector3(x, boxY, z) + transform.position;
            boxes[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        for (int i = 0; i < numRamps; i++)
        {
            int id = i + NumHiders + NumSeekers + numBoxes;
            float x = itemPlacement[id].Item1.x;
            float z = itemPlacement[id].Item1.y;
            if (instantiateInteractables)
            {
                Ramp rampPrefab = rampPrefabs[Random.Range(0, rampPrefabs.Length)];
                ramps[i] = Instantiate(rampPrefab);
            }
            ramps[i].transform.position = new Vector3(x, rampY, z) + transform.position;
            ramps[i].transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    private bool TryPlaceObject(List<(Vector2, float)> itemPlacement, Func<Vector2> pickPointFn, float radius, int numTries)
    {
        for (int _try = 0; _try < numTries; _try++)
        {
            Vector2 point = pickPointFn.Invoke();
            bool correct = true;
            for (int i = 0; i < itemPlacement.Count; i++)
            {
                if (Vector2.Distance(itemPlacement[i].Item1, point) < itemPlacement[i].Item2 + radius)
                {
                    correct = false;
                    break;
                }
            }

            if (correct)
            {
                itemPlacement.Add((point, radius));
                return true;
            }
        }
        return false;
    }

    private Vector2 PickPointAnywhere(float margin)
    {
        float v = mapSize * 0.5f - margin;
        return PickPointRect(-v, v, -v, v);
    }

    private Vector2 PickPointRoom(float margin)
    {
        float u = mapSize * 0.5f - roomSize + margin;
        float v = mapSize * 0.5f - margin;
        return PickPointRect(u, v, -v, -u);
    }

    private Vector2 PickPointOutside(float margin)
    {
        float u = mapSize * 0.5f - roomSize;
        float v = mapSize * 0.5f;
        
        float x1A = u + margin;
        float x2A = v - margin;
        float z1A = -u + margin;
        float z2A = v - margin;
        float areaA = (x2A - x1A) * (z2A - z1A);

        float x1B = -v + margin;
        float x2B = u - margin;
        float z1B = -v + margin;
        float z2B = v - margin;
        float areaB = (x2B - x1B) * (z2B - z1B);

        return Random.Range(0f, areaA + areaB) < areaA ? PickPointRect(x1A, x2A, z1A, z2A)
                                                       : PickPointRect(x1B, x2B, z1B, z2B);
    }

    private Vector2 PickPointRect(float minX, float maxX, float minZ, float maxZ)
    {
        return new Vector2(Random.Range(minX, maxX), Random.Range(minZ, maxZ));
    }

    private Vector2 PickPointHider()
    {
        return generateSubroom ? PickPointRoom(0.5f * wallThickness + agentRadius)
                               : PickPointAnywhere(0.5f * wallThickness + agentRadius);
    }

    private Vector2 PickPointSeeker()
    {
        return generateSubroom ? PickPointOutside(0.5f * wallThickness + agentRadius)
                               : PickPointAnywhere(0.5f * wallThickness + agentRadius);
    }

    private Vector2 PickPointBox()
    {
        if (generateSubroom)
        {
            return Random.Range(0, 2) == 0 ? PickPointRoom(0.5f * wallThickness + objectRadius)
                                           : PickPointOutside(0.5f * wallThickness + objectRadius);
        }
        else
        {
            return PickPointAnywhere(0.5f * wallThickness + objectRadius);
        }
    }

    private Vector2 PickPointRamp()
    {
        return generateSubroom ? PickPointOutside(0.5f * wallThickness + objectRadius)
                               : PickPointAnywhere(0.5f * wallThickness + objectRadius);
    }


    public bool InstantiatesInteractables() => instantiateInteractables;
}
