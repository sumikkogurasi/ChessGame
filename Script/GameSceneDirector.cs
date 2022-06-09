using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// チェスシステム えびふらいちゃん！ 2022/06/05
/// </summary>
public class GameSceneDirector : MonoBehaviour
{
    // ゲーム設定
    public const int TILE_X = 8;
    public const int TILE_Y = 8;

    const int PLAYER_MAX = 2;

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
        //{2, 0, 0, 0, 0, 0, 11, 12 },
        //{0, 0, 0, 0, 0, 0, 11, 13 },
        //{0, 0, 0, 0, 0, 0, 11, 14 },
        //{0, 0, 0, 0, 0, 0, 11, 15 },
        //{6, 0, 0, 0, 0, 0, 11, 16 },
        //{0, 0, 0, 0, 0, 0, 11, 14 },
        //{0, 0, 0, 0, 0, 0, 11, 13 },
        //{2, 0, 0, 0, 0, 0, 11, 12 },
    };

    // UI関連
    GameObject txtTurnInfo;
    GameObject txtResultInfo;
    GameObject btnApply;
    GameObject btnCancel;

    // 選択中のユニット
    UnitController selectUnit;

    // 移動関連
    List<Vector2Int> movableTiles;
    List<GameObject> cursors;

    // モード
    enum MODE
    {
        NONE,
        CHECK_MATE,
        NORMAL,
        STATUS_UPDATE,
        TURN_CHANGE,
        RESULT,
    }

    MODE nowMode, nextMode;
    int nowPlayer;

    // 前回ユニット削除から経過ターン
    int prevDestroyTurn;

    // 前回の盤面
    List<UnitController[,]> prevUnits;

    void Start()
    {
        // オブジェクト取得
        txtTurnInfo = GameObject.Find("TextTurnInfo");
        txtResultInfo = GameObject.Find("TextResultInfo");
        btnApply = GameObject.Find("ButtonApply");
        btnCancel = GameObject.Find("ButtonCancel");

        //Result系は消しておく
        btnApply.SetActive(false);
        btnCancel.SetActive(false);

        // 移動関連
        cursors = new List<GameObject>();

        // 内部データ
        tiles = new GameObject[TILE_X, TILE_Y];
        units = new UnitController[TILE_X, TILE_Y];
        prevUnits = new List<UnitController[,]>();
        movableTiles = new List<Vector2Int>();

        // タイルを作成(unity上ではX-Z平面)
        for (int i = 0; i < TILE_X; i++)
        {
            for(int j = 0; j < TILE_Y; j++)
            {
                // 座標原点をワールド座標原点から左下へ移動
                // タイルとユニットのポジション
                float x = i - TILE_X / 2;
                float y = j - TILE_Y / 2;

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

        // 初期モード
        nowPlayer = -1;
        nowMode = MODE.NONE;
        nextMode = MODE.TURN_CHANGE;
    }

    void Update()
    {
        if(MODE.CHECK_MATE == nowMode)
        {
            checkMateMode();
        }
        else if(MODE.NORMAL == nowMode)
        {
            // ノーマルモード
            normalMode();
        }
        else if(MODE.STATUS_UPDATE == nowMode)
        {
            statusUpdateMode();
        }
        else if(MODE.TURN_CHANGE == nowMode)
        {
            turnChangeMode();
        }

        // モード変更
        if(MODE.NONE != nextMode)
        {
            nowMode = nextMode;
            nextMode = MODE.NONE; 
        }
    }

    void checkMateMode()
    {
        // 次のモード
        nextMode = MODE.NORMAL;

        Text info = txtResultInfo.GetComponent<Text>();
        info.text = "";

        // ------------------------
        // ドローのチェック（簡易版）
        // ------------------------

        // 1 vs 1になったら引き分け
        // ユニットの組み合わせでチェックメイトできない状態があるらしいがとりあえず
        if(3 > getUnits().Count)
        {
            info.text = "チェックメイトできないので\n引き分け";
            nextMode = MODE.RESULT;
        }

        // 50ターンの誰も死ななかったらドロー
        if(50 < prevDestroyTurn)
        {
            info.text = "50ターンルールで\n引き分け";
            nextMode = MODE.RESULT;
        }

        // 3回同じ盤面になったらドロー
        int prevcount = 0;

        // 1盤面ずつチェック
        foreach(var v in prevUnits)
        {
            bool check = true;

            for(int i = 0; i < v.GetLength(0); i++)
            {
                for(int j = 0; j < v.GetLength(1); j++)
                {
                    // 昔の盤面と今回の盤面が違ったらcheckは偽
                    if (v[i, j] != units[i, j]) check = false;
                }
            }

            if (check) prevcount++;
        }

        // 三回続いたら
        if(2 < prevcount)
        {
            info.text = "同じ盤面が続いたので\n引き分け";
            nextMode = MODE.RESULT;
        }

        // ------------------------
        // チェックのチェック
        // ------------------------
        // 今回のプレイヤーのキング
        UnitController target = getUnit(nowPlayer, UnitController.TYPE.KING);
        // チェックしているユニット
        List<UnitController> checkunits = GetCheckUnits(units, nowPlayer);

        // チェック状態セット
        bool ischeck = (0 < checkunits.Count) ? true : false;

        if(null != target)
        {
            target.SetCheckStatus(ischeck);
        }

        // ゲームが続くならチェックと表示
        if( ischeck && MODE.RESULT != nextMode)
        {
            info.text = "チェック！！";

        }

        // ------------------------
        // 移動可能範囲を調べる
        // ------------------------
        // チェック状態から動かせないならチェックメイト
        // ただ動かせないならステイルメイト
        int tilecount = 0;

        // 移動可能範囲取得
        foreach(var v in getUnits(nowPlayer))
        {
            tilecount += getMovableTiles(v).Count;
        }

        // 動かせない
        if(1 > tilecount)
        {
            if (ischeck)
            {
                info.text = "チェックメイト\n"
                    + (getNextPlayer() + 1) + "Pの勝ち！！";
            }
            else
            {
                info.text = "ステイルメイト\n"
                + "引き分け";
            }

            nextMode = MODE.RESULT;
        }

        // 今回の盤面をコピー
        UnitController[,] copyunits = new UnitController[units.GetLength(0), units.GetLength(1)];
        Array.Copy(units, copyunits, units.Length);
        prevUnits.Add(copyunits);

        // 次のモードの準備
        if (MODE.RESULT == nextMode)
        {
            btnApply.SetActive(true);
            btnCancel.SetActive(true);
        }
    }

    // ノーマルモード
    void normalMode()
    {
        GameObject tile = null;
        UnitController unit = null;

        // プレイヤー
        if (Input.GetMouseButtonUp(0))
        {
            // カメラ位置からレイを飛ばしてスクリーン位置を取得（x, y）
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // ユニットにも当たり判定があるのでヒットした全てのオブジェクト情報を取得
            foreach (RaycastHit hit in Physics.RaycastAll(ray))
            {
                if (hit.transform.name.Contains("Tile"))
                {
                    tile = hit.transform.gameObject;
                    break;
                }
            }
        }

        // CPU（ただランダムに駒を動かすだけ）
        while(TitleSceneDirector.PlayerCount <= nowPlayer
            && (null == selectUnit || null == tile))
        {
            // ユニット選択
            if(null == selectUnit)
            {
                // 今回の全ユニット
                List<UnitController> tmpunits = getUnits(nowPlayer);

                // ランダムで1体選ぶ
                UnitController tmp = tmpunits[Random.Range(0, tmpunits.Count)];

                // ユニットがいるタイルを選択
                tile = tiles[tmp.Pos.x, tmp.Pos.y];

                // 一旦処理へ流すためbreak
                break;
            }

            // ここからはselectUnitが入った状態で来る
            if( 1 > movableTiles.Count)
            {
                // もう一回
                setSelectCursors();
                break;
            }

            // 移動可能範囲が在ればランダムで移動
            int rnd = Random.Range(0, movableTiles.Count);
            tile = tiles[movableTiles[rnd].x, movableTiles[rnd].y];
        }

        // タイルが押されていなければ処理しない
        if (null == tile) return;

        // 選んだタイルからユニット取得
        Vector2Int tilepos = new Vector2Int(
            (int)tile.transform.position.x + TILE_X / 2,
            (int)tile.transform.position.z + TILE_Y / 2
            );

        // タイルに乗っているユニット
        unit = units[tilepos.x, tilepos.y];

        // ユニット選択
        if (         null != unit 
            && selectUnit != unit
            && nowPlayer  == unit.Player
            )
        {
            // 移動可能範囲をセットする
            List<Vector2Int> tiles = getMovableTiles(unit);

            // 選択不可能
            if (1 > tiles.Count) return;

            movableTiles = tiles;

            // カーソルの描画と選択したユニット
            setSelectCursors(unit);
        }

        // 移動 クリックしたタイルがmovableTilesに含まれていたら駒を移動する
        else if (null != selectUnit && movableTiles.Contains(tilepos))
        {
            moveUnit(selectUnit, tilepos);
            nextMode = MODE.STATUS_UPDATE;
        }

        // 移動範囲だけ見られる
        else if (null != unit && nowPlayer != unit.Player)
        {
            setSelectCursors(unit, false);
        }
        // 選択解除
        else
        {
            setSelectCursors();
        }
    }

    // 移動後の処理
    // ステータスをアップデートするモード
    void statusUpdateMode()
    {
        // キャスリング
        if (selectUnit.Status.Contains(UnitController.STATUS.QSIDE_CASTLING))
        {
            // 左端のルーク
            UnitController unit = units[0, selectUnit.Pos.y];
            Vector2Int tile = new Vector2Int(selectUnit.Pos.x + 1, selectUnit.Pos.y);

            unit.Status.Add(UnitController.STATUS.QSIDE_CASTLING);
            moveUnit(unit, tile);
        }
        else if (selectUnit.Status.Contains(UnitController.STATUS.KSIDE_CASTLING))
        {
            // 右端のルーク
            UnitController unit = units[TILE_X - 1, selectUnit.Pos.y];
            Vector2Int tile = new Vector2Int(selectUnit.Pos.x - 1, selectUnit.Pos.y);

            unit.Status.Add(UnitController.STATUS.KSIDE_CASTLING);
            moveUnit(unit, tile);
        }
        // アンパッサン
        foreach(var v in getUnits(getNextPlayer()))
        {
            if (!v.Status.Contains(UnitController.STATUS.EN_PASSANT)) continue;

            // 置いた場所がアンパッサン対象か
            if(selectUnit.Pos == v.OldPos)
            {
                Destroy(v.gameObject);
            }
        }
        // プロモーション
        if(UnitController.TYPE.PAWN == selectUnit.Type)
        {
            int py = TILE_Y - 1;
            if (selectUnit.Player == 1) py = 0;

            // 端に到達
            if(py == selectUnit.Pos.y)
            {
                GameObject prefab = getPrefabUnit(nowPlayer, (int)UnitController.TYPE.QUEEN);
                UnitController unit = Instantiate(prefab).GetComponent<UnitController>();
                GameObject tile = tiles[selectUnit.Pos.x, selectUnit.Pos.y];

                // とりあえずクイーン固定
                unit.SetUnit(selectUnit.Player, UnitController.TYPE.QUEEN, tile);
                moveUnit(unit, new Vector2Int(selectUnit.Pos.x, selectUnit.Pos.y));
            }
        }

        // ターン経過
        foreach(var v in getUnits(nowPlayer))
        {
            v.ProgressTurn();
        }

        // カーソル
        setSelectCursors();

        nextMode = MODE.TURN_CHANGE;
    }

    // 相手のターン変更
    // ターンをチェンジするモード
    void turnChangeMode()
    {
        // ターンの処理
        nowPlayer = getNextPlayer();

        // Info更新
        txtTurnInfo.GetComponent<Text>().text = "" + (nowPlayer + 1) + "Pの番です";

        // 経過ターン（1P側に来たら＋1）
        if(0 == nowPlayer)
        {
            prevDestroyTurn++;
        }

        nextMode = MODE.CHECK_MATE;
    }

    // 次のプレイヤー番号の取得
    int getNextPlayer()
    {
        int next = nowPlayer + 1;
        if (PLAYER_MAX <= next) next = 0;

        return next;
    }

    // 指定ユニット
    UnitController getUnit(int player, UnitController.TYPE type)
    {
        foreach(var v in getUnits(player))
        {
            if (player != v.Player) continue;
            if (type == v.Type) return v;
        }

        return null;
    }

    // 指定されたプレイヤーのユニットを取得
    List<UnitController> getUnits(int player = -1)
    {
        List<UnitController> ret = new List<UnitController>();

        foreach(var v in units)
        {
            if(null == v) continue;

            if(player == v.Player)
            {
                ret.Add(v);
            }
            else if(0 > player)
            {
                ret.Add(v);
            }
        }

        return ret;
    }
    // 移動可能範囲を取得
    List<Vector2Int> getMovableTiles(UnitController unit)
    {
        // そこをどいたらチェックされてしまうか
        UnitController[,] copyunits = new UnitController[units.GetLength(0), units.GetLength(1)];
        Array.Copy(units, copyunits, units.Length);
        copyunits[unit.Pos.x, unit.Pos.y] = null;

        // チェックされてるかどうか
        List<UnitController> checkunits = GetCheckUnits(copyunits, unit.Player);

        // チェックを回避できるタイルを返す
        if(0 < checkunits.Count)
        {
            // 移動可能タイル
            List<Vector2Int> ret = new List<Vector2Int>();

            // 移動可能範囲
            List<Vector2Int> movetiles = unit.GetMovableTiles(units);

            // チェック中のユニットを邪魔できる場所を探す
            foreach(var v in movetiles)
            {
                // 移動した状態を作る
                UnitController[,] copyunits2 = new UnitController[units.GetLength(0), units.GetLength(1)];
                Array.Copy(units, copyunits2, units.Length);
                
                // 移動してみる
                copyunits2[unit.Pos.x, unit.Pos.y] = null;
                copyunits2[v.x, v.y] = unit;

                int checkcount = GetCheckUnits(copyunits2, unit.Player, false).Count;

                if (1 > checkcount) ret.Add(v);
            }

            return ret;
        }

        // 通常移動可能範囲を返す
        return unit.GetMovableTiles(units);
    }

    // カーソルの描画と選択したユニット
    void setSelectCursors(UnitController unit = null, bool setunit = true)
    {
        // カーソル解除
        foreach(var v in cursors)
        {
            Destroy(v);
        }

        // 選択ユニットの非選択状態
        if(null != selectUnit)
        {
            selectUnit.SelectUnit(false);
            selectUnit = null;
        }

        // 何もセットしないなら終わり
        if(null == unit) return;

        // カーソル作成
        foreach(var v in getMovableTiles(unit))
        {
            Vector3 pos = tiles[v.x, v.y].transform.position;
            pos.y += 0.51f;
            GameObject obj = Instantiate(prefabCursor, pos, Quaternion.identity);
            cursors.Add(obj);
        }

        // 選択状態
        if (setunit)
        {
            selectUnit = unit;
            selectUnit.SelectUnit(setunit);
        }
    }

    // ユニット移動
    bool moveUnit(UnitController unit, Vector2Int tilepos)
    {
        Vector2Int unitpos = unit.Pos;

        // 誰かが居たら消す（相手の駒を取る）
        if (null != units[tilepos.x, tilepos.y])
        {
            // コンポーネント（UnitController）が付いているゲームオブジェクトを破壊
            Destroy(units[tilepos.x, tilepos.y].gameObject);
            prevDestroyTurn = 0;
        }

        // 新しい場所へ移動
        unit.MoveUnit(tiles[tilepos.x, tilepos.y]);

        // 配列データの更新（元の場所）
        units[unitpos.x, unitpos.y] = null;
        
        // 配列データの更新（新しい場所）
        units[tilepos.x, tilepos.y] = unit;

        return true;
    }

    // 指定された配置でチェックされているか調べる　
    static public List <UnitController> GetCheckUnits(UnitController[,] units, int player, bool checkking = true)
    {
        List<UnitController> ret = new List<UnitController>();

        // 敵プレイヤーの移動可能範囲にキングがいるかどうか
        foreach(var v in units)
        {
            if (null == v) continue;
            if (player == v.Player) continue;

            // 敵の1体の移動可能範囲
            List<Vector2Int> enemytiles = v.GetMovableTiles(units, checkking);

            foreach( var t in enemytiles)
            {
                if(null == units[t.x, t.y]) continue;
                if(UnitController.TYPE.KING == units[t.x, t.y].Type)
                {
                    ret.Add(v);
                }
            }
        }
        return ret;
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

    public void Retry()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void Title()
    {
        SceneManager.LoadScene("TitleScene");
    }
}
