using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using NyARUnityUtils;
using jp.nyatla.nyartoolkit.cs.core;
using jp.nyatla.nyartoolkit.cs.markersystem;

/// <summary>
/// NyARマーカーで作られた箱を見つけて座標変換する
/// </summary>
public class NyARBoxFinder : MonoBehaviour {

    /// <summary>
    /// カメラデバイスの名前、無効な文字列の場合は最初のカメラを使用
    /// </summary>
    public string CameraName = "";
    /// <summary>
    /// カメラ画像の幅
    /// </summary>
    public int CameraWidth = 640;
    /// <summary>
    /// カメラ画像の高さ
    /// </summary>
    public int CameraHeight = 480;
    /// <summary>
    /// カメラのFPS
    /// </summary>
    public int CameraFps = 30;
    /// <summary>
    /// マーカーの1辺のサイズ
    /// </summary>
    public float MarkerSize = 0.05f;

    /// <summary>
    /// 見つけたARBoxの座標変換を適用するオブジェクト
    /// </summary>
    public GameObject TransformTarget;

    /// <summary>
    /// 指数移動平均のパラメータ
    /// </summary>
    public float EmaParameter = 0.2f;

    ///////////////////////////////////////////////////////

    NyARUnityMarkerSystem _markerSystem;
    NyARUnityWebCam _nyarWebCam;

    Vector3 _position = Vector3.zero;
    Quaternion _quaternion = Quaternion.identity;

    /// <summary>
    /// NyIDマーカーと立方体面の変換対応
    /// 位置は正規化された値で、利用時に立方体の1辺の値でスケールされる
    /// </summary>
    Dictionary<int, PosRot> _nyIdPosRotMap = new Dictionary<int, PosRot>()
        {
            {0, new PosRot(0, 0, 0.5f, 0, 180, 90)},      //上面 
            {20, new PosRot(-0.5f, 0, 0, 0, -90, 0)},     //右面 
            {50, new PosRot(0.5f, 0, 0, 0, 90, 0)},       //左面 
            {40, new PosRot(0, 0, -0.5f, 0, 0, 90)},      //下面 
            {10, new PosRot(0, -0.5f, 0, 0, -90, -90)},   //前面 
        };

    Dictionary<int, int> _nyIdMap = new Dictionary<int, int>();

	void Start()
    {
        var devices = WebCamTexture.devices;
        if (devices.Length > 0)
        {
            //取得できたカメラデバイスをリスト
            Debug.Log(
                devices.Length + " camera devices were found.\n" +
                string.Join("\n", devices.Select(d => " - " + d.name).ToArray()));
        }
        else
        {
            Debug.LogError("no camera device was found.");
            Debug.Break();
        }

        //カメラを取得、無効な組み合わせの場合は最初のカメラが取得される
        //var webcam = new WebCamTexture(CameraName, CameraWidth, CameraHeight, CameraFps);
        var webcam = new WebCamTexture(640, 480, 30);
        Debug.Log(
            webcam.deviceName + " was selected.\n" + 
            webcam.requestedWidth + "x" + webcam.requestedHeight + "x" + webcam.requestedFPS + " is reqested.");

        //NyARの初期化
        _nyarWebCam = new NyARUnityWebCam(webcam);
        _markerSystem = new NyARUnityMarkerSystem(new NyARMarkerSystemConfig(webcam.requestedWidth, webcam.requestedHeight));

        //NyIDマーカーの登録
        foreach (var k in _nyIdPosRotMap.Keys)
        {
            _nyIdMap.Add(_markerSystem.addNyIdMarker(k, MarkerSize), k);
        }

        //カメラ画像取得開始
        _nyarWebCam.start();

        //[Debug]
        //var bg = GameObject.Find("Plane");
        //bg.renderer.material.mainTexture = webcam;
        //_markerSystem.setARBackgroundTransform(bg.transform);
        //_markerSystem.setARCameraProjection(camera);
	}
	
	void Update() 
    {
        _nyarWebCam.update();
	    _markerSystem.update(_nyarWebCam);

        //登録されている中で見つかったマーカーを探す
        foreach (var id in _nyIdMap.Keys)
        {
            if (_markerSystem.isExistMarker(id))
            {
                var pos = Vector3.zero;
                var quat = Quaternion.identity;
                _markerSystem.getMarkerTransform(id, ref pos, ref quat);

                var posrot = _nyIdPosRotMap[_nyIdMap[id]];
                pos += posrot.Position * MarkerSize; //マーカー1辺の長さで正規化
                quat *= Quaternion.Euler(posrot.Rotation);

                if (isNaN(pos, quat))
                    break;

                _position = Vector3.Slerp(_position, pos, EmaParameter);
                _quaternion = Quaternion.Slerp(_quaternion, quat, EmaParameter);
            }
        }

        if (TransformTarget != null)
        {
            TransformTarget.transform.localPosition = _position;
            TransformTarget.transform.localRotation = _quaternion;
        }
	}

    bool isNaN(Vector3 pos, Quaternion quat)
    {
        return
            float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
            float.IsNaN(quat.w) || float.IsNaN(quat.x) || float.IsNaN(quat.y) || float.IsNaN(quat.z);
    }

    class PosRot
    {
        /// <summary>
        /// 位置
        /// </summary>
        public Vector3 Position;
        /// <summary>
        /// 回転
        /// </summary>
        public Vector3 Rotation;

        /// <summary>
        /// 位置と回転角度を与えてインスタンスを生成
        /// </summary>
        /// <param name="px">位置X</param>
        /// <param name="py">位置Y</param>
        /// <param name="pz">位置Z</param>
        /// <param name="rx">回転X</param>
        /// <param name="ry">回転Y</param>
        /// <param name="rz">回転Z</param>
        public PosRot(float px, float py, float pz, float rx, float ry, float rz)
        {
            Position.x = px;
            Position.y = py;
            Position.z = pz;
            Rotation.x = rx;
            Rotation.y = ry;
            Rotation.z = rz;
        }
    }
}
