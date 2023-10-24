using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.TextCore.Text;
using UnityEditor;
using Unity.VisualScripting;
using UnityEngine.UIElements;

public class MapGen : MonoBehaviour {

	public static int mapWidth = 50;
	public static int mapHeight = 25;
	public static int mapOverdraw = 50;

	private Dictionary<string, int[]> tileSups = new();
	private Dictionary<string, int> setTiles = new();
	private Dictionary<string, int> oldTileSupCounts = new();
	private Dictionary<string, (string, string)[]> facingDirections = new();
	private Dictionary<string, Dictionary<string, int>> tileWeights = new();
	private Dictionary<int, Tile> loadedTiles = new();

	enum TT { // Tile Type
		Grass,
		Water,
		Trees,
		Hills,
	}

	enum TG { // Tile Group
		None,
		Grass,
		TreesTipTopLeft,
		TreesTipTopRight,
		TreesTipBottomLeft,
		TreesTipBottomRight,
	}

	private Tile recGreen;
	private Tile recRed;
	private Tile recBlue;
	private Tile recGrey;

	private TileObject[] tileObjects;
	private Grid grid;
	private Tilemap tilemapNoCollision;
	private Tilemap tilemapCollision;
	private Tilemap tilemapHighlights;
	private readonly System.Random r = new();

	class TileObject {
		public Dictionary<string, TT> terrainTypes;
		public int[] tileNbrs;
		public TT tileClass;
		public TG tileGroup;
		public string name;
		public TileObject(TT topLeft, TT top, TT topRight, TT left, TT right, TT bottomLeft, TT bottom, TT bottomRight, int[] tileNbrs, string name, TT tileClass, TG tileGroup = 0) {
			terrainTypes = new Dictionary<string, TT> {
				{ "topLeft",     topLeft },
				{ "top",         top },
				{ "topRight",    topRight },
				{ "left",        left },
				{ "right",       right },
				{ "bottomLeft",  bottomLeft },
				{ "bottom",      bottom },
				{ "bottomRight", bottomRight },
			};
			this.tileNbrs = tileNbrs;
			this.tileClass = tileClass;
			this.tileGroup = tileGroup;
			this.name = name;
		}
	}

	// Start is called before the first frame update
	void Start() {
		grid = GetComponent<Grid>();

		// Console.OutputEncoding = System.Text.Encoding.UTF8;
		// Console.WriteLine("Hello World! \u263A ✅ 👨‍👩‍👧‍👦 💾");

		tilemapNoCollision = GameObject.Find("Tilemap-NoCollision").GetComponent<Tilemap>();
		tilemapCollision = GameObject.Find("Tilemap-Collision").GetComponent<Tilemap>();
		tilemapHighlights = GameObject.Find("Tilemap-Highlights").GetComponent<Tilemap>();

		// BoundsInt bounds = tilemapNoCollision.cellBounds;
		// TileBase[] allTiles = tilemapNoCollision.GetTilesBlock(bounds);

		// TileBase tile0 = GameObject.Find("/Art/Tiles/punyworld-overworld-tileset_0.asset").GetComponent<TileBase>();

		recGreen = Resources.Load<Tile>("Tiles/punyworld-overworld-tileset_96");
		recRed = Resources.Load<Tile>("Tiles/punyworld-overworld-tileset_97");
		recBlue = Resources.Load<Tile>("Tiles/punyworld-overworld-tileset_116");
		recGrey = Resources.Load<Tile>("Tiles/punyworld-overworld-tileset_117");

		facingDirections = new Dictionary<string, (string, string)[]> {
			// NOTE: As the algorithm stands now, we should only ever compare directly adjacent tiles, so we don't need to check for diagonals.
			{ "-1 1", new (string, string)[] {("bottomRight", "topLeft")} }, // tile is on top-left relative to source tile
			{ "1 1", new (string, string)[] {("bottomLeft", "topLeft")} }, // tile is on top-right relative to source tile
			{ "-1 -1", new (string, string)[] {("topRight", "bottomLeft")} }, // tile is on bottom-left relative to source tile
			{ "1 -1", new (string, string)[] {("topLeft", "bottomLeft")} }, // tile is on bottom-right relative to source tile
			{ "0 1", new (string, string)[] {("bottomLeft", "topLeft"), ("bottom", "top"), ("bottomRight", "topRight")} }, // tile is on top relative to source tile
			{ "0 -1", new (string, string)[] {("topLeft", "bottomLeft"), ("top", "bottom"), ("topRight", "bottomRight")} }, // tile is on bottom relative to source tile
			{ "-1 0", new (string, string)[] {("topRight", "topLeft"), ("right", "left"), ("bottomRight", "bottomLeft")} }, // tile is on left relative to source tile
			{ "1 0", new (string, string)[] {("topLeft", "topRight"), ("left", "right"), ("bottomLeft", "bottomRight")} }, // tile is on right relative to source tile
		};

		tileObjects = new TileObject[] {
			//  TopLeft     Top     TopRight    Left      Right  BottomLeft  Bottom  BottomRight         TileNbrs      Name       TileClass  TypeGroup
			new(TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, new int[] { 218 }, "Water Normal", TT.Water),

			// Shores

			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0 }, "Grass Blank", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 1 }, "Grass Bushy 1", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 2 }, "Grass Bushy 2", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 23 }, "Grass Bushy 3", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 24 }, "Grass Bushy 4", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 25 }, "Grass Bushy 5", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 50 }, "Grass Bushy 6", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 51 }, "Grass Bushy 7", TT.Grass, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 52 }, "Grass Bushy 8", TT.Grass, TG.Grass),

			new(TT.Grass, TT.Grass, TT.Grass, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, new int[] { 191 }, "Grass-Shore Top", TT.Grass),
			new(TT.Grass, TT.Water, TT.Water, TT.Grass, TT.Water, TT.Grass, TT.Water, TT.Water, new int[] { 217 }, "Grass-Shore Left", TT.Grass),
			new(TT.Water, TT.Water, TT.Grass, TT.Water, TT.Grass, TT.Water, TT.Water, TT.Grass, new int[] { 219 }, "Grass-Shore Right", TT.Grass),
			new(TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Grass, TT.Grass, TT.Grass, new int[] { 243 }, "Grass-Shore Bottom", TT.Grass),

			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Water, TT.Grass, TT.Water, TT.Water, new int[] { 190 }, "Grass-Shore Top-Left", TT.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Water, TT.Grass, TT.Water, TT.Water, TT.Grass, new int[] { 192 }, "Grass-Shore Top-Right", TT.Grass),
			new(TT.Grass, TT.Water, TT.Water, TT.Grass, TT.Water, TT.Grass, TT.Grass, TT.Grass, new int[] { 242 }, "Grass-Shore Bottom-Left", TT.Grass),
			new(TT.Water, TT.Water, TT.Grass, TT.Water, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 244 }, "Grass-Shore Bottom-Right", TT.Grass),

			new(TT.Grass, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, new int[] { 193 }, "Grass-Shore Tip Top-Left", TT.Grass),
			new(TT.Water, TT.Water, TT.Grass, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, new int[] { 194 }, "Grass-Shore Tip Top-Right", TT.Grass),
			new(TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Grass, TT.Water, TT.Water, new int[] { 220 }, "Grass-Shore Tip Bottom-Left", TT.Grass),
			new(TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Water, TT.Grass, new int[] { 221 }, "Grass-Shore Tip Bottom-Right", TT.Grass),

			// Trees:

			new(TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Trees, new int[] { 150 }, "Trees", TT.Trees),

			new(TT.Trees, TT.Trees, TT.Trees, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 168 }, "Trees Top on Grass", TT.Trees),
			new(TT.Trees, TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Trees, TT.Grass, TT.Grass, new int[] { 0, 151 }, "Trees Left on Grass", TT.Trees),
			new(TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Trees, TT.Grass, TT.Grass, TT.Trees, new int[] { 0, 149 }, "Trees Right on Grass", TT.Trees),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Trees, TT.Trees, TT.Trees, new int[] { 0, 132 }, "Trees Bottom on Grass", TT.Trees),

			new(TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Grass, TT.Trees, TT.Grass, TT.Grass, new int[] { 0, 156 }, "Trees Top-Left on Grass", TT.Trees),
			new(TT.Trees, TT.Trees, TT.Trees, TT.Grass, TT.Trees, TT.Grass, TT.Grass, TT.Trees, new int[] { 0, 155 }, "Trees Top-Right on Grass", TT.Trees),
			new(TT.Trees, TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Trees, TT.Trees, TT.Trees, new int[] { 0, 138 }, "Trees Bottom-Left on Grass", TT.Trees),
			new(TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Trees, TT.Trees, TT.Trees, TT.Trees, new int[] { 0, 137 }, "Trees Bottom-Right on Grass", TT.Trees),

			new(TT.Trees, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 169 }, "Trees Tip Top-Left 1 on Grass", TT.Trees, TG.TreesTipTopLeft),
			new(TT.Trees, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 172 }, "Trees Tip Top-Left 2 on Grass", TT.Trees, TG.TreesTipTopLeft),
			new(TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 167 }, "Trees Tip Top-Right 1 on Grass", TT.Trees, TG.TreesTipTopRight),
			new(TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 170 }, "Trees Tip Top-Right 2 on Grass", TT.Trees, TG.TreesTipTopRight),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Grass, new int[] { 0, 133 }, "Trees Tip Bottom-Left 1 on Grass", TT.Trees, TG.TreesTipBottomLeft),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Trees, TT.Grass, TT.Grass, new int[] { 0, 136 }, "Trees Tip Bottom-Left 2 on Grass", TT.Trees, TG.TreesTipBottomLeft),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Trees, new int[] { 0, 131 }, "Trees Tip Bottom-Right 1 on Grass", TT.Trees, TG.TreesTipBottomRight),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Trees, new int[] { 0, 134 }, "Trees Tip Bottom-Right 2 on Grass", TT.Trees, TG.TreesTipBottomRight),

			new(TT.Grass, TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Trees, TT.Grass, new int[] { 0, 134 }, "Trees Diagonal Top-Right Bottom-Left on Grass", TT.Trees),
			new(TT.Trees, TT.Trees, TT.Grass, TT.Trees, TT.Trees, TT.Grass, TT.Trees, TT.Trees, new int[] { 0, 134 }, "Trees Diagonal Top-Left Bottom-Right on Grass", TT.Trees),

			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 139 }, "Tree Center on Grass", TT.Trees, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 157 }, "Tree Left on Grass", TT.Trees, TG.Grass),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 0, 175 }, "Tree Right on Grass", TT.Trees, TG.Grass),

			// Hills:

			new(TT.Hills, TT.Hills, TT.Hills, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 119 }, "Hills Top", TT.Hills),
			new(TT.Hills, TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Hills, TT.Grass, TT.Grass, new int[] { 100 }, "Hills Left", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Hills, TT.Grass, TT.Grass, TT.Hills, new int[] { 98 }, "Hills Right", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Hills, TT.Hills, TT.Hills, new int[] { 82 }, "Hills Bottom", TT.Hills),

			new(TT.Hills, TT.Hills, TT.Hills, TT.Hills, TT.Grass, TT.Hills, TT.Grass, TT.Grass, new int[] { 102 }, "Hills Top-Left", TT.Hills),
			new(TT.Hills, TT.Hills, TT.Hills, TT.Grass, TT.Hills, TT.Grass, TT.Grass, TT.Hills, new int[] { 101 }, "Hills Top-Right", TT.Hills),
			new(TT.Hills, TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Hills, TT.Hills, TT.Hills, new int[] { 85 }, "Hills Bottom-Left", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Hills, TT.Hills, TT.Hills, TT.Hills, new int[] { 84 }, "Hills Bottom-Right", TT.Hills),

			new(TT.Hills, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 120 }, "Hills Tip Top-Left", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 118 }, "Hills Tip Top-Right", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Grass, new int[] { 83 }, "Hills Tip Bottom-Left", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Hills, new int[] { 81 }, "Hills Tip Bottom-Right", TT.Hills),

			new(TT.Grass, TT.Hills, TT.Hills, TT.Hills, TT.Hills, TT.Hills, TT.Hills, TT.Grass, new int[] { 122 }, "Hills Diagonal Top-Right Bottom-Left", TT.Hills),
			new(TT.Hills, TT.Hills, TT.Grass, TT.Hills, TT.Hills, TT.Grass, TT.Hills, TT.Hills, new int[] { 121 }, "Hills Diagonal Top-Left Bottom-Right", TT.Hills),

			new(TT.Hills, TT.Hills, TT.Hills, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 123 }, "Hills Stairs Top", TT.Hills),
			new(TT.Hills, TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Hills, TT.Grass, TT.Grass, new int[] { 104 }, "Hills Stairs Left", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Hills, TT.Grass, TT.Hills, TT.Grass, TT.Grass, TT.Hills, new int[] { 103 }, "Hills Stairs Right", TT.Hills),
			new(TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Hills, TT.Hills, TT.Hills, new int[] { 86 }, "Hills Stairs Bottom", TT.Hills),

			new(TT.Hills, TT.Hills, TT.Hills, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 95 }, "Hills Mine Top", TT.Hills),
			new(TT.Hills, TT.Hills, TT.Hills, TT.Grass, TT.Grass, TT.Grass, TT.Grass, TT.Grass, new int[] { 94 }, "Hills Mine Covered Top", TT.Hills),
		};

		tileWeights = new Dictionary<string, Dictionary<string, int>> {
			{ "default", new Dictionary<string, int> {
				{ "Water Normal", 10 },
				{ "Grass Blank", 5 },
				{ "Grass Bushy 1", 5 },
				{ "Grass Bushy 2", 5 },
				{ "Grass Bushy 3", 5 },
				{ "Grass Bushy 4", 5 },
				{ "Grass Bushy 5", 5 },
				{ "Grass Bushy 6", 5 },
				{ "Grass Bushy 7", 5 },
				{ "Grass Bushy 8", 5 },
				{ "Grass-Shore Top", 10 },
				{ "Grass-Shore Bottom", 10 },
				{ "Grass-Shore Left", 10 },
				{ "Grass-Shore Right", 10 },
				{ "Hills Stairs Top", 1 },
				{ "Hills Stairs Left", 1 },
				{ "Hills Stairs Right", 1 },
				{ "Hills Stairs Bottom", 1 },
				{ "Hills Mine Top", 1 },
				{ "Hills Mine Covered Top", 1 },
			}},
			{ "mountain", new Dictionary<string, int> {
				{ "Grass Blank", 10 },
			}},
		};

		tilemapNoCollision.ClearAllTiles();

		// TileObject tileObject = Array.Find(tileObjects, tileObj => tileObj.name == "Water Normal");
		// tileObjects.ToList().FindIndex(tileObj => tileObj.name == "Water Normal");

		// Fill the map with placeholder tiles:
		// tilemapNoCollision.SetTilesBlock(new BoundsInt(0, 0, 0, mapWidth, mapHeight, 1), Enumerable.Repeat(recGrey, mapWidth * mapHeight).ToArray());

		// tilemapNoCollision.SetTile(new Vector3Int(0, 0, 0), GetTile(23));

		// Set all tiles to -1 (no tile):
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				setTiles.Add(x + "-" + y, -1);
			}
		}

		WFC_Init();
		GenerateOverdraw();
		WFC_Start();
	}

	// Update is called once per frame
	void Update() {}

	void GenerateOverdraw() {
		// Draw the overdraw tiles:
		Tile overdrawTile = GetTile(218); // Water Normal
		TileBase[] topBottomTiles = Enumerable.Repeat(overdrawTile, (mapWidth + mapOverdraw * 2) * mapOverdraw).ToArray();
		TileBase[] leftRightTiles = Enumerable.Repeat(overdrawTile, mapOverdraw * mapHeight).ToArray();
		tilemapNoCollision.SetTilesBlock(new BoundsInt(-mapOverdraw, -mapOverdraw, 0, mapWidth + mapOverdraw * 2, mapOverdraw, 1), topBottomTiles); // bottom with corners
		tilemapNoCollision.SetTilesBlock(new BoundsInt(-mapOverdraw, mapHeight, 0, mapWidth + mapOverdraw * 2, mapOverdraw, 1), topBottomTiles); // top with corners
		tilemapNoCollision.SetTilesBlock(new BoundsInt(-mapOverdraw, 0, 0, mapOverdraw, mapHeight, 1), leftRightTiles); // left
		tilemapNoCollision.SetTilesBlock(new BoundsInt(mapWidth, 0, 0, mapOverdraw, mapHeight, 1), leftRightTiles); // right

		int overdrawTileIndex = tileObjects.ToList().FindIndex(tileObj => tileObj.name == "Water Normal");

		// Save the current entropy of all tiles:
		SaveCurrentEntropy();
		// Iterate around the map to set/prepare the necessary information for the WFC check algorithm:
		for (int x = -1; x < mapWidth + 1; x++) { // top and bottom with corners
			// Top row with corners:
			tileSups.Add(x + "--1", new int[] { overdrawTileIndex });
			setTiles[x + "--1"] = overdrawTileIndex;
			// Bottom row with corners:
			tileSups.Add(x + "-" + mapHeight, new int[] { overdrawTileIndex });
			setTiles[x + "-" + mapHeight] = overdrawTileIndex;
		}
		for (int y = 0; y < mapHeight; y++) { // left and right
			// Left column:
			tileSups.Add("-1-" + y, new int[] { overdrawTileIndex });
			setTiles["-1-" + y] = overdrawTileIndex;
			// Right column:
			tileSups.Add(mapWidth + "-" + y, new int[] { overdrawTileIndex });
			setTiles[mapWidth + "-" + y] = overdrawTileIndex;
		}

		// Iterate around the map again to check and reduce the entropy of all tiles that are close to the map border:
		for (int x = -1; x < mapWidth + 1; x++) { // top and bottom with corners
			// Top row with corners:
			WFC_CheckNeighbors(x, -1);
			// Bottom row with corners:
			WFC_CheckNeighbors(x, mapHeight);
		}
		for (int y = 0; y < mapHeight; y++) { // left and right
			// Left column:
			WFC_CheckNeighbors(-1, y);
			// Right column:
			WFC_CheckNeighbors(mapWidth, y);
		}

	}

	void WFC_Init() {
		// Before starting the WFC algorithm, we need to set the initial entropy of all tiles to the full superset:
		int[] fullEntropy = Enumerable.Range(0, tileObjects.Length).ToArray();
		for (int x = 0; x < mapWidth; x++) {
			for (int y = 0; y < mapHeight; y++) {
				tileSups.Add(x + "-" + y, fullEntropy);
			}
		}
	}

	void WFC_Start() {
		// Save the current entropy of all tiles:
		SaveCurrentEntropy();
		// Set a random tile at a random position:
		int inset = 4; // arbitrary inset to prevent the tile from being placed at the edge of the map
		int randomX = r.Next(inset, mapWidth - inset);
		int randomY = r.Next(inset, mapHeight - inset);
		int randomTileIndex = r.Next(tileObjects.Length);
		tileSups[randomX + "-" + randomY] = new int[] { randomTileIndex };
		SetTile(randomX, randomY, randomTileIndex);
		StartCoroutine(WFC_Steps(randomX, randomY));
	}

	IEnumerator WFC_Steps(int startX, int startY) {
		int lowestEntropy = int.MinValue; // this starting value is only important for the first iteration and will be overwritten immediately
		int currentX = startX;
		int currentY = startY;

		while (lowestEntropy == int.MinValue || lowestEntropy != int.MaxValue) { // exit condition
			print("-------------------- NEW WFC STEP: [" + currentX + ", " + currentY + "] --------------------");
			// Check and reduce the entropy of all tiles around the current tile (wich is the last tile that has been set):
			WFC_CheckNeighbors(currentX, currentY);
			// Create a dictionary that bundles all tiles with the same entropy in one "bucket":
			Dictionary<int, Dictionary<string, int[]>> tileSupsByEntropy = new();
			lowestEntropy = int.MaxValue;
			foreach (var sups in tileSups) { // iterate over all tiles
				int entropy = CalcUniqueTilePossibilities(sups.Value); // get the entropy of the current tile
				if (entropy < lowestEntropy && entropy > 1) lowestEntropy = entropy; // if current entropy is lower than lowestEntropy (but not 1), set lowestEntropy to current entropy
				tileSupsByEntropy.TryAdd(entropy, new()); // create a new bucket for the current entropy if it doesn't exist yet
				tileSupsByEntropy[entropy].Add(sups.Key, sups.Value); // put the tile into the bucket of its entropy
			}

			// Draw all tiles that are fully collapsed (entropy = 1):
			// print("Nbr of fully collapsed tiles: " + tileSupsByEntropy[1].Count);
			foreach (var fullyCollapsedTile in tileSupsByEntropy[1]) {
				if (setTiles[fullyCollapsedTile.Key] != -1) continue; // skip if tile is already set
				// Get x and y coordinates from the key:
				(int x, int y) = GetCoordinates(fullyCollapsedTile.Key);
				int fullyCollapsedTileIndex = GetWeightedRandomTile(fullyCollapsedTile.Value); // choose a random tile from the superset (this is only necessary if all remaining tiles have are in the same tile group)
				tileSups[fullyCollapsedTile.Key] = new int[] { fullyCollapsedTileIndex }; // set the superset to the fully collapsed tile
				// print("Tile [" + x + ", " + y + "] is fully collapsed!");
				SetTile(x, y, fullyCollapsedTileIndex);
			}

			// Break early if exit condition is already met:
			if (lowestEntropy == int.MaxValue) break;
			// Get a random tile from the lowest entropy bucket:
			KeyValuePair<string, int[]> nextTile = tileSupsByEntropy[lowestEntropy].ElementAt(r.Next(tileSupsByEntropy[lowestEntropy].Count));
			(int nextX, int nextY) = GetCoordinates(nextTile.Key);
			int nextTileIndex = GetWeightedRandomTile(nextTile.Value);
			// Save the current entropy of all tiles:
			SaveCurrentEntropy();
			// Set the selcted tile:
			tileSups[nextTile.Key] = new int[] { nextTileIndex };
			SetTile(nextX, nextY, nextTileIndex);
			 // Wait one frame before continuing:
			yield return null;
			// Set the coordinates for the next iteration:
			currentX = nextX;
			currentY = nextY;
		}
	}

	void WFC_CheckNeighbors(int x, int y) {
		int checkDistance = 3; // how far to check for neighbors. This is an arbitrary value that largely depends on the tileset (max. min. distance between all two tiles in the tileset)

		// Check all tiles in a circle (or rather a diamond shape) around the source tile:
		// Note: we don't actually need to iterate around the entire circle, but only down from the top position until one step before the right position. The rest of the positions can be checked be mirroring our current position around the source tile or the x/y axis. This means that we only need to check 1/4 of the circle, but in every step we need to check 4 tiles instead of 1.
		for (int dist = 1; dist <= checkDistance; dist++) {
			// Set the starting position (top):
			int dX = 0;
			int dY = dist;
			for (int i = 0; i < dist; i++) {
				WFC_Collapse(x + dX, y + dY, x, y); // top-right quadrant
				WFC_Collapse(x + dY, y - dX, x, y); // bottom-right quadrant
				WFC_Collapse(x - dX, y - dY, x, y); // bottom-left quadrant
				WFC_Collapse(x - dY, y + dX, x, y); // top-left quadrant
				// Iterate to the next position (diagonally to the bottom right):
				dX++;
				dY--;
			}
		}
	}

	void WFC_Collapse(int x, int y, int sourceX, int sourceY) {
		// print("Collapsing [" + x + ", " + y + "]");
		if (x >= 0 && x < mapWidth && y >= 0 && y < mapHeight) { // check if target is in bounds
			if (tileSups[x + "-" + y].Length == 1) return; // skip if own entropy is already fully collapsed
			// if (y - sourceY != 0) print("Compare to " + (y - sourceY > 0 ? "bottom" : "top") + " neighbor");
			if (y - sourceY != 0) WFC_Compare(x, y, x, y - sourceY > 0 ? y - 1 : y + 1); // check top/bottom neighbor
			// if (x - sourceX != 0) print("Compare to " + (x - sourceX > 0 ? "left" : "right") + " neighbor");
			if (x - sourceX != 0) WFC_Compare(x, y, x - sourceX > 0 ? x - 1 : x + 1, y); // check left/right neighbor
		}
	}

	void WFC_Compare(int x, int y, int neighborX, int neighborY) {
		// Get a list of all possible tiles for the current tile and the neighbor tile:
		int[] ownSups = tileSups[x + "-" + y];
		int[] neighborSups = tileSups[neighborX + "-" + neighborY];

		// Check if neighbor-compare tile has reduced its entropy and skip the comparison if not:
		// if (oldTileSupCounts[neighborX + "-" + neighborY] == neighborSups.Length) return; // TODO fix this god damn check

		// Check each tile in ownSups against each tile in neighborSups to only keep those tiles that are possible to place given the neighbor tiles:
		bool[] possibleToPlace = new bool[ownSups.Length]; // array of bools to keep track of which tiles are still possible to place and which should be removed from the superset
		for (int i = 0; i < ownSups.Length; i++) {
			int ownSup = ownSups[i];
			foreach (int neighborSup in neighborSups) {
				// Check all (3) facing directions between the two tiles:
				int directionsToCheck = facingDirections[x - neighborX + " " + (y - neighborY)].Length;
				foreach ((string, string) dir in facingDirections[x - neighborX + " " + (y - neighborY)]) {
					TT ownTerrainType = tileObjects[ownSup].terrainTypes[dir.Item1];
					TT neighborTerrainType = tileObjects[neighborSup].terrainTypes[dir.Item2];
					// print(tileObjects[ownSup].name + ": [" + dir.Item1 + "] " + ownTerrainType + "<->" + neighborTerrainType + " [" + dir.Item2 + "] ==> " + (ownTerrainType == neighborTerrainType ? "MATCH!" : "NO MATCH :("));
					// Check if the terrain types match and if not, abort the comparison for this tile:
					if (ownTerrainType == neighborTerrainType) directionsToCheck--;
					else break;
				}
				// If all directions match, the tile is possible to place:
				if (directionsToCheck == 0) {
					possibleToPlace[i] = true;
					// print(tileObjects[ownSup].name + "is possible to place!!!");
					break; // we only need one match to keep the tile in the superset
				}
			}
		}
		int[] reducedSups = ownSups.Where((sup, i) => possibleToPlace[i]).ToArray();
		// print("Reduced [" + x + ", " + y + "] from " + ownSups.Length + " to " + reducedSups.Length + " (compared to d[" + (neighborX - x) + ", " + (neighborY - y) + "])");
		if (reducedSups.Length == 0) {
			print("ERROR: No possibilities left for [" + x + ", " + y + "]");
			tileSups[x + "-" + y] = reducedSups;
			DrawTile(x ,y, recRed);
			return;
		} else if (reducedSups.Length != ownSups.Length) {
			tileSups[x + "-" + y] = reducedSups;
			DrawTile(x ,y, recGreen);
		} else if (reducedSups.Length == oldTileSupCounts[x + "-" + y]) {
			DrawTile(x, y, recBlue);
		}
	}

	void OnGUI() {
		Camera cam = Camera.main;
		Event currentEvent = Event.current;
		Vector2 mousePos = new() {
			x = currentEvent.mousePosition.x,
			y = cam.pixelHeight - currentEvent.mousePosition.y // the y position from Event is inverted
		};

		Vector3 point = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));

		// GUI.Box(new Rect(Screen.width / 2 - 400, 20, 820, 30), "");
		// GUI.Label(new Rect(Screen.width / 2 - 30, 25, 410, 30), "GUI Test", guiStyle);

		// VisualElement root = GameObject.Find("UIDocument").GetComponent<UIDocument>().rootVisualElement;
		// VisualElement container = root.Q<VisualElement>("Container");
		// container.Add(new Label("Hello World!"));
		// print(root.Children().First().name + " " + root.Children().First().Children().First().name);

		int mouseX = (int)Math.Floor(point.x / grid.cellSize.x);
		int mouseY = (int)Math.Floor(point.y / grid.cellSize.y);
		tilemapHighlights.ClearAllTiles();
		tilemapHighlights.SetTile(new Vector3Int(mouseX, mouseY, 0), recBlue);
		// print("x: " + (int)Math.Floor(point.x / grid.cellSize.x) + " y: " + (int)Math.Floor(point.y / grid.cellSize.y));

		GUILayout.BeginArea(new Rect(10, 10, 250, 580));
		GUILayout.Box($"X: {mouseX}  Y: {mouseY}");
		GUILayout.Box($"Possible tiles: {(tileSups.ContainsKey(mouseX + "-" + mouseY) ? tileSups[mouseX + "-" + mouseY].Length : 0)} / {tileObjects.Length}\nPossible unique tile shapes: {(tileSups.ContainsKey(mouseX + "-" + mouseY) ? CalcUniqueTilePossibilities(tileSups[mouseX + "-" + mouseY]) : 0)}\n\n{(tileSups.ContainsKey(mouseX + "-" + mouseY) ? string.Join('\n', tileSups[mouseX + "-" + mouseY].Select(sup => tileObjects[sup].name)) : "")}");
		// print(string.Join(", ", tileSups.Keys.ToArray()));
		// GUILayout.Label("Mouse position: " + mousePos);
		GUILayout.EndArea();
	}

	Tile GetTile(int id) {
		Tile tile;
		loadedTiles.TryGetValue(id, out tile);
		if (tile == null) {
			tile = Resources.Load<Tile>("Tiles/punyworld-overworld-tileset_" + id);
			loadedTiles.Add(id, tile);
		}
		return tile;
	}

	void DrawTile(int x, int y, Tile tile) {
		tilemapNoCollision.SetTile(new Vector3Int(x, y, 0), tile);
	}
	void DrawTile(int x, int y, int tileIndex) {
		int[] tiles = tileObjects[tileIndex].tileNbrs;
		for (int i = 0; i < tiles.Length; i++) {
			tilemapNoCollision.SetTile(new Vector3Int(x, y, i), GetTile(tiles[i]));
		}
	}

	// void SetTile(int x, int y, Tile tile) {
	// 	setTiles[x + "-" + y] = Array.IndexOf(tileObjects, tileObjects.First(t => t.tile == tile));
	// 	DrawTile(x, y, tile);
	// }
	void SetTile(int x, int y, int tileIndex) {
		setTiles[x + "-" + y] = tileIndex;
		DrawTile(x, y, tileIndex);
	}

	(int, int) GetCoordinates(string key) {
		string[] coordinates = key.Split("-");
		return (int.Parse(coordinates[0]), int.Parse(coordinates[1]));
	}

	void SaveCurrentEntropy() {
		oldTileSupCounts.Clear();
		foreach (var sups in tileSups) {
			oldTileSupCounts.Add(sups.Key, sups.Value.Length);
		}
	}

	int GetWeight(string biome, string tileName) {
		int weight = 0;
		tileWeights[biome].TryGetValue(tileName, out weight);
		if (weight == 0) {
			tileWeights["default"].TryGetValue(tileName, out weight);
			if (weight == 0) weight = 10;
		}
		return weight;
	}

	int GetWeightedRandomTile(int[] sups) {
		// Write the coulative weigts of all sups into an array:
		int[] comulativeSupWeights = new int[sups.Length];
		int totalWeight = 0;
		for (int i = 0; i < sups.Length; i++) {
			int weight = GetWeight("default", tileObjects[sups[i]].name);
			totalWeight += weight;
			comulativeSupWeights[i] = totalWeight;
		}
		// Get a (weighted) random tile from the superset:
		int randomValue = r.Next(totalWeight);
		// print("Next tile: [" + nextX + ", " + nextY + "]");
		// print("Random value: " + randomValue + " (total weight: " + totalWeight + ")");
		// print("Comulative weights: " + string.Join(", ", comulativeSupWeights));
		int index = comulativeSupWeights.ToList().FindIndex(comWeight => randomValue < comWeight);
		// print("Chosen index: " + index);
		int randomTileIndex = sups[index];
		// print("Chosen tile: " + tileObjects[randomTileIndex].name + " (" + randomTileIndex + ")");
		return randomTileIndex;
	}

	int CalcUniqueTilePossibilities(int[] sups) {
		HashSet<TG> alreadyCountedTileGroups = new();
		int uniqueTilePossibilities = 0;
		foreach (int sup in sups) {
			TG tileGroup = tileObjects[sup].tileGroup;
			if (tileGroup == TG.None) {
				uniqueTilePossibilities++;
			} else if (!alreadyCountedTileGroups.Contains(tileGroup)) {
				uniqueTilePossibilities++;
				alreadyCountedTileGroups.Add(tileGroup);
			}
		}
		return uniqueTilePossibilities;
	}

}
