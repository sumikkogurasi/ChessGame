using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneDirector : MonoBehaviour
{
    // ゲーム全体のプレイヤー数
    static public int PlayerCount = 2;

    // タイルのプレハブ
    public GameObject[] prefabTile;

    // カーソルのプレハブ
    public GameObject prefabCursor;

    // 内部データ
    GameObject[,] tiles;
    UnitController[,] units;

    // ユニットのプレハブ（色ごと）（インスペクタ上で二次元配列を表示するには？）
    public List<GameObject> prefabWhiteUnits;
    public List<GameObject> prefabBlackUnits;


    // 1 = ポーン　2 = ルーク 3 = ナイト 4 = ビショップ 5 = クイーン 6 = キング
    // チェスは線対称 手前側は一桁、奥側が二桁

    public int[,] unitType =
    {
        {2, 1, 0, 0, 0, 0, 11, 12 },
        {3, 1, 0, 0, 0, 0, 11, 13 },
        {4, 1, 0, 0, 0, 0, 11, 14 },
        {5, 1, 0, 0, 0, 0, 11, 15 },
        {6, 1, 0, 0, 0, 0, 11, 16 },
        {4, 1, 0, 0, 0, 0, 11, 14 },
        {3, 1, 0, 0, 0, 0, 11, 13 },
        {2, 1, 0, 0, 0, 0, 11, 12 },
    };

    // Start is called before the first frame update
    void Start()
    {
        // 内部データ
        tiles = new GameObject[GameSceneDirector.TILE_X, GameSceneDirector.TILE_Y];
        units = new UnitController[GameSceneDirector.TILE_X, GameSceneDirector.TILE_Y];

        // タイルを作成(unity上ではX-Z平面)
        for (int i = 0; i < GameSceneDirector.TILE_X; i++)
        {
            for (int j = 0; j < GameSceneDirector.TILE_Y; j++)
            {
                // 座標原点をワールド座標原点から左下へ移動
                // タイルとユニットのポジション
                float x = i - GameSceneDirector.TILE_X / 2;
                float y = j - GameSceneDirector.TILE_Y / 2;

                // Z軸をY軸と見做す
                Vector3 pos = new Vector3(x, 0, y);

                // インスタンス生成 （白黒のチェックタイル）
                int idx = (i + j) % 2;
                GameObject tile = Instantiate(prefabTile[idx], pos, Quaternion.identity);

                // 内部データとして二次元配列で保存
                tiles[i, j] = tile;

                // ユニットの作成
                // 余り（駒の種類）
                int type = unitType[i, j] % 10;
                // 商（プレイヤー番号）
                int player = unitType[i, j] / 10;

                // 番号から選ぶプレハブを決める
                GameObject prefab = getPrefabUnit(player, type);
                GameObject unit = null;
                UnitController ctrl = null;

                // nullだったら下の処理を飛ばす
                if (null == prefab) continue;

                unit = Instantiate(prefab);

                // 初期設定
                ctrl = unit.GetComponent<UnitController>();
                ctrl.SetUnit(player, (UnitController.TYPE)type, tile);

                // 内部データセット
                units[i, j] = ctrl;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // ユニットのプレハブを返す（playerは０～１、typeは１～６）
    GameObject getPrefabUnit(int player, int type)
    {
        // 0からスタート（type０～５に変換）
        int idx = type - 1;

        if (0 > idx) return null;

        // プレイヤー番号が０⇒白色の駒のリスト
        GameObject prefab = prefabWhiteUnits[idx];

        // プレイヤー番号が１⇒黒色の駒のリスト
        if (1 == player) prefab = prefabBlackUnits[idx];

        return prefab;
    }

    public void PvP()
    {
        PlayerCount = 2;
        SceneManager.LoadScene("SampleScene");
    }

    public void PvE()
    {
        PlayerCount = 1;
        SceneManager.LoadScene("SampleScene");
    }
    public void EvE()
    {
        PlayerCount = 0;
        SceneManager.LoadScene("SampleScene");
    }
}
