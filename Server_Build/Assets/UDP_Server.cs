using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

public struct EndPoint_Connection {
    //クライアントのIP
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string connectionIP;
    //クライアントのID
    public int ID;
}

public struct PositionSync {
    public Vector3 position;
    public int ID;
}

public class UDP_Server : MonoBehaviour
{
    //クライアント情報
    private List<EndPoint_Connection> connections = new List<EndPoint_Connection>();

    //初期接続用のソケット割り当て
    private const int firstConnection_port = 50764;
    private Socket firstConnection_socket = null;

    private const int positionSync_port = 50766;
    private const int send_port = 50767;
    private Socket positionSync_socket = null;
    private List<PositionSync> positionSyncs_Que = new List<PositionSync>();

    // 接続先のIPアドレス.
    private string			address = "";
	
	// 接続先のポート番号.
	private const int 		m_port = 50765;

	// 通信用変数
	private Socket			socket = null;

	// 状態.
	private State			state;

	// 状態定義
	private enum State
	{
		SelectHost = 0,
		CreateListener,
		ReceiveMessage,
		CloseListener,
		SendMessage,
		Endcommunication,
	}


	// Use this for initialization
	void Start ()
	{

		IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());
		System.Net.IPAddress hostAddress = hostEntry.AddressList[0];
		Debug.Log(hostEntry.HostName);
		address = hostAddress.ToString();

        CreateListener();

        var thread = new Thread(loop);
        thread.Start();
    }

    private void FixedUpdate() {
    }

    // Update is called once per frame
    void Update ()
	{
        switch (state) {
            case State.CreateListener:
                //CreateListener();
                break;

            case State.ReceiveMessage:
                //ReceivePosition();
                break;

            case State.CloseListener:
                CloseListener();
                break;

            case State.SendMessage:
                //SendPosition();
                break;

            default:
                break;
        }

        Debug.Log(positionSyncs_Que.Count);

        SendPosition();

        WaitConnection();
    }

    public void WaitConnection() {
        byte[] buffer = new byte[1400];
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint senderRemote = (EndPoint)sender;

        if (firstConnection_socket.Poll(0, SelectMode.SelectRead)) {
            int recvSize = firstConnection_socket.ReceiveFrom(buffer, SocketFlags.None, ref senderRemote);
            if (recvSize > 0) {
                GCHandle gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                EndPoint_Connection temp = (EndPoint_Connection)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(EndPoint_Connection));
                bool t = false;
                for (int i = 0; i < connections.Count; i++) {
                    if (connections[i].connectionIP.Equals(temp.connectionIP)) {
                        t = true;
                        break;
                    }
                }

                if (t == false) {
                    connections.Add(temp);
                    Debug.Log("Add new end point:" + temp.connectionIP);
                }
                gch.Free();
            }
        }
    }

    void loop() {
        while(true){
            ReceivePosition();
        }
    }

    void ReceivePosition() {
        byte[] buffer = new byte[1400];
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint senderRemote = (EndPoint)sender;

        if (positionSync_socket.Poll(0, SelectMode.SelectRead)) {
            int recvSize = positionSync_socket.ReceiveFrom(buffer, SocketFlags.None, ref senderRemote);
            if (recvSize > 0) {
                GCHandle gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                PositionSync temp = (PositionSync)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(PositionSync));
                gch.Free();

                positionSyncs_Que.Add(temp);

                //Debug.Log("Receive position:" + temp.position);
            }
        }
        state = State.SendMessage;
    }

    /// <summary>
    /// 位置情報を参加者全員に送信
    /// </summary>
    void SendPosition() {
        //
        if (positionSyncs_Que.Count > 0) {

            int size = Marshal.SizeOf(positionSyncs_Que[0]);

            byte[] bytes = new byte[size];

            GCHandle gchw = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Marshal.StructureToPtr(positionSyncs_Que[0], gchw.AddrOfPinnedObject(), false);
            gchw.Free();

            int i = 0;
            while (i < connections.Count) {
                string client_address = connections[i].connectionIP;

                Socket send_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(client_address), send_port);

                send_socket.SendTo(bytes, bytes.Length, SocketFlags.None, endpoint);
                i++;
            }
            

            positionSyncs_Que.RemoveAt(0);
        }

        state = State.ReceiveMessage;
    }

    // 他の端末からのメッセージ受信.
    void CreateListener()
	{
		Debug.Log("[UDP]Start communication.");
		
		// ソケットを生成します.
		socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        // 使用するポート番号を割り当てます.
        socket.Bind(new IPEndPoint(IPAddress.Any, m_port));

        //初期接続用
        firstConnection_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        firstConnection_socket.Bind(new IPEndPoint(IPAddress.Any, firstConnection_port));

        //位置同期用
        positionSync_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        positionSync_socket.Bind(new IPEndPoint(IPAddress.Any, positionSync_port));

        state = State.ReceiveMessage;
    }

	
	// 待ち受け終了.
	void CloseListener()
	{	
		// 待ち受けを終了します.
		if (socket != null) {
			socket.Close();
			socket = null;
		}

		state = State.Endcommunication;

		Debug.Log("[UDP]End communication.");
	}
}
