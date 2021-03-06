﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Web.Script.Serialization;
using Model;
using Service;

public partial class Form1 : Form
{
    DeviceModel Device;
    private AdvantechHttpWebUtility m_HttpRequest;
    DataHandleService dataHld;
    bool DevConnFail = false;
    ServiceAction servAct = new ServiceAction();

    private delegate void DataGridViewCtrlAddDataRow(DataGridViewRow i_Row);
    private DataGridViewCtrlAddDataRow m_DataGridViewCtrlAddDataRow;
    internal const int Max_Rows_Val = 65535;

    SysData GetDataArry = new SysData();
    SysChgData ChangeDataArry = new SysChgData();//change description content
    bool changeFlg = false;
    wResult ExeRes;

    iALibrary.iAS ias = new iALibrary.iAS();


    public Form1()
    {
        InitializeComponent();

        ChangeDataArry = new SysChgData()
        {
            Md = 0,
            ISSID = "123456789012345678901234567890AB",//32
            ISec = 2,
            IKey = "123456789012345678901234567890123456789012345678901234567890ABC",//63
            ISSID2 = "123456789012345678901234567890AB",//32
            ISec2 = 2,
            IKey2 = "123456789012345678901234567890123456789012345678901234567890ABC",//63
            ASSID = "123456789012345678901234567890AB",//32
            AHid = 1,
            ACnty = 2,
            ACh = 11,
            ASec = 2,
            AKey = "123456789012345678901234567890123456789012345678901234567890ABC",//63
            DHCP = 1,
            IP = "192.168.0.68",
            Msk = "255.255.255.0",
            GW = "192.168.0.1",
        };
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        //Get IO value
        m_HttpRequest = new AdvantechHttpWebUtility();
        m_HttpRequest.ResquestOccurredError += this.OnGetHttpRequestError;
        m_HttpRequest.ResquestResponded += this.OnGetData;
        //
        Device = new DeviceModel()//20150626 建立一個DeviceModel給所有Service
        {
            IPAddress = textBox1.Text,
            Account = "root",
            Password = "00000000",
            Port = 80,
            SlotNum = 0,
            ModbusAddr = 1,
            ModbusTimeOut = 3000,
        };
        //
        dataGridView1.ColumnHeadersVisible = true;
        DataGridViewTextBoxColumn newCol = new DataGridViewTextBoxColumn(); // add a column to the grid
        newCol.HeaderText = "Time";
        newCol.Name = "clmTs";
        newCol.Visible = true;
        newCol.Width = 50;
        dataGridView1.Columns.Add(newCol);
        //
        newCol = new DataGridViewTextBoxColumn();
        newCol.HeaderText = "Method";
        newCol.Name = "clmStp";
        newCol.Visible = true;
        newCol.Width = 50;
        dataGridView1.Columns.Add(newCol);
        //
        newCol = new DataGridViewTextBoxColumn();
        newCol.HeaderText = "Instruction";
        newCol.Name = "clmIns";
        newCol.Visible = true;
        newCol.Width = 100;
        dataGridView1.Columns.Add(newCol);        
        //
        newCol = new DataGridViewTextBoxColumn();
        newCol.HeaderText = "Description";
        newCol.Name = "clmDes";
        newCol.Visible = true;
        newCol.Width = 100;
        dataGridView1.Columns.Add(newCol);
        //
        newCol = new DataGridViewTextBoxColumn();
        newCol.HeaderText = "Result";
        newCol.Name = "clmRes";
        newCol.Visible = true;
        newCol.Width = 80;
        dataGridView1.Columns.Add(newCol);
        //
        newCol = new DataGridViewTextBoxColumn();
        newCol.HeaderText = "Error";
        newCol.Name = "clmErr";
        newCol.Visible = true;
        newCol.Width = 100;
        dataGridView1.Columns.Add(newCol);

        for (int i = 0; i < dataGridView1.Columns.Count - 1; i++)
        {
            dataGridView1.Columns[i].SortMode = DataGridViewColumnSortMode.Automatic;
        }
        dataGridView1.Rows.Clear();
        try
        {
            m_DataGridViewCtrlAddDataRow = new DataGridViewCtrlAddDataRow(DataGridViewCtrlAddNewRow);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }

        dataHld = new DataHandleService();
        textBox1.Text = dataHld.GetPara(Application.StartupPath);
        //debug
        //GetNetConfigRequest();
    }

    protected override void WndProc(ref System.Windows.Forms.Message iMessage)
    {
        int iType = (int)iMessage.WParam;

        if (iMessage.Msg == ias.MsgExcutionCtrlCommand)
        {
            switch (iType)
            {
                case (int)iALibrary.iCommand.Start:
                    RunProc();
                    break;
                case (int)iALibrary.iCommand.Stop:

                    ias.iStatus(iALibrary.iStatus.Stop);
                    break;
            }
        }
        base.WndProc(ref iMessage);
    }

    private void RunProc()
    {
        ias.iStatus(iALibrary.iStatus.Running);
        textBox1.Text = dataHld.GetPara(Application.StartupPath);
        GetNetConfigRequest();
    }

    private string GetURL(string ip, int port, string requestUri)
    {
        return "http://" + ip + ":" + port.ToString() + "/" + requestUri;
    }

    private void DataGridViewCtrlAddNewRow(DataGridViewRow i_Row)
    {
        if (this.dataGridView1.InvokeRequired)
        {
            this.dataGridView1.Invoke(new DataGridViewCtrlAddDataRow(DataGridViewCtrlAddNewRow), new object[] { i_Row });
            return;
        }

        this.dataGridView1.Rows.Insert(0, i_Row);
        if (dataGridView1.Rows.Count > Max_Rows_Val)
        {
            dataGridView1.Rows.RemoveAt((dataGridView1.Rows.Count - 1));
        }
        this.dataGridView1.Update();
    }

    private void OnGetHttpRequestError(Exception ex)
    {
        ExeRes.Res = ExeCaseRes.Fail;
        ExeRes.Err = ex.ToString();
        Print(ExeRes);
    }

    private void OnGetData(string rawData)//Feedback Http request
    {
        switch (servAct)
        {
            case ServiceAction.GetNetConfig:
                var Obj01 = AdvantechHttpWebUtility.ParserJsonToObj<SysData>(rawData);
                UpdateDevUIStatus(Obj01);
                //
                ExeRes.Res = ExeCaseRes.Pass;Print(ExeRes);
                this.InvokeWaitStep();
                break;
            case ServiceAction.PatchSysInfo:
                break;
            case ServiceAction.GetNetConfig_ag:
                var Obj03 = AdvantechHttpWebUtility.ParserJsonToObj<SysData>(rawData);
                UpdateDevUIStatus(Obj03);
                //
                ExeRes.Res = ExeCaseRes.Pass; Print(ExeRes);
                this.InvokeWaitStep();
                break;
        }
    }

    void Print(wResult obj)
    {
        DataGridViewRow dgvRow;
        DataGridViewCell dgvCell;
        dgvRow = new DataGridViewRow();
        //dgvRow.DefaultCellStyle.Font = new Font(this.Font, FontStyle.Regular);
        dgvCell = new DataGridViewTextBoxCell(); //Column Time
        var dataTimeInfo = DateTime.Now.ToString("yyyy-MM-dd HH:MM:ss");
        dgvCell.Value = dataTimeInfo;
        dgvRow.Cells.Add(dgvCell);
        //
        dgvCell = new DataGridViewTextBoxCell();
        dgvCell.Value = obj.Method;
        dgvRow.Cells.Add(dgvCell);
        //
        dgvCell = new DataGridViewTextBoxCell();
        dgvCell.Value = obj.Ins;
        dgvRow.Cells.Add(dgvCell);
        //
        dgvCell = new DataGridViewTextBoxCell();
        dgvCell.Value = obj.Des;
        dgvRow.Cells.Add(dgvCell);
        //
        dgvCell = new DataGridViewTextBoxCell();
        dgvCell.Value = obj.Res;
        dgvRow.Cells.Add(dgvCell);
        //
        dgvCell = new DataGridViewTextBoxCell();
        dgvCell.Value = obj.Err;
        dgvRow.Cells.Add(dgvCell);

        m_DataGridViewCtrlAddDataRow(dgvRow);

        ExeRes = new wResult();
    }

    private void button1_Click(object sender, EventArgs e)
    {
        GetNetConfigRequest();
    }

    private void InvokeWaitStep()//start to read IO invoke
    {
        int m_iPollingTime = 500;
        Thread.Sleep(m_iPollingTime);//m_iPollingTime
        NextStep();
    }

    private void NextStep()
    {
        if (servAct == ServiceAction.GetNetConfig_ag)
        {
            VerifyItems();
        }
        else if (servAct == ServiceAction.PatchSysInfo)
        {
            GetNetConfigRequest();
        }
        else if (servAct == ServiceAction.GetNetConfig)
        {
            PatchSysInfoRequest();
        }
    }

    //Request Cmd
    private void GetNetConfigRequest()
    {
        Print(new wResult() { Des = "GetNetConfigRequest" });
        dataHld.SavePara(Application.StartupPath, textBox1.Text);
        Device.IPAddress = textBox1.Text;
        servAct = ServiceAction.GetNetConfig;
        m_HttpRequest.SendGETRequest(Device.Account, Device.Password,
                                        "http://" + Device.IPAddress + "/wlan_config");
        //
        if (changeFlg) servAct = ServiceAction.GetNetConfig_ag;
        //
        ExeRes = new wResult()
        {
            Method = HttpRequestOption.GET,
            Ins = WISE_RESTFUL_URI.wlan_config,
        };
        
    }

    private void PatchSysInfoRequest()//Patch info
    {
        Print(new wResult() { Des = "PatchSysInfoRequest" });
        servAct = ServiceAction.PatchSysInfo;

        JavaScriptSerializer serializer = new JavaScriptSerializer();
        string sz_Jsonify = serializer.Serialize(ChangeDataArry);

        m_HttpRequest.SendPATCHRequest(Device.Account, Device.Password, GetURL(Device.IPAddress, Device.Port
                                    , WISE_RESTFUL_URI.wlan_config.ToString()), sz_Jsonify);
        changeFlg = true;
        //
        ExeRes = new wResult()
        {
            Method = HttpRequestOption.PATCH,
            Ins = WISE_RESTFUL_URI.wlan_config,
            Res = ExeCaseRes.Pass,
        }; Print(ExeRes);

        this.InvokeWaitStep();
    }

    private void VerifyItems()
    {
        int errorCnt = 0; changeFlg = false;
        Print(new wResult() { Des = "VerifyItems" });
        bool chk = false;
        chk = false;
        if (GetDataArry.Md != ChangeDataArry.Md) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "Md  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ISSID != ChangeDataArry.ISSID) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ISSID  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ISec != ChangeDataArry.ISec) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ISec  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.IKey != ChangeDataArry.IKey) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "IKey  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ISSID2 != ChangeDataArry.ISSID2) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ISSID2  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ISec2 != ChangeDataArry.ISec2) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ISec2  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.IKey2 != ChangeDataArry.IKey2) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "IKey2  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ASSID != ChangeDataArry.ASSID) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ASSID  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.AHid != ChangeDataArry.AHid) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "AHid  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ACnty != ChangeDataArry.ACnty) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ACnty  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ACh != ChangeDataArry.ACh) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ACh  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.ASec != ChangeDataArry.ASec) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "ASec  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.AKey != ChangeDataArry.AKey) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "AKey  check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        //
        chk = false;
        if (GetDataArry.DHCP != ChangeDataArry.DHCP) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "DHCP check", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.IP != ChangeDataArry.IP) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "IP check [" + GetDataArry.IP + "]", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.Msk != ChangeDataArry.Msk) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "Msk check [" + GetDataArry.Msk + "]", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });
        chk = false;
        if (GetDataArry.GW != ChangeDataArry.GW) { chk = true; errorCnt++; }
        Print(new wResult() { Des = "GW check [" + GetDataArry.GW + "]", Res = chk ? ExeCaseRes.Fail : ExeCaseRes.Pass });

        //Return the test result
        if (errorCnt > 0) ias.ReturnRes((int)iALibrary.iResult.Fail);
        else ias.ReturnRes((int)iALibrary.iResult.Pass);
        //To notify iATester the test is completion
        ias.iStatus(iALibrary.iStatus.Completion);
    }

    #region ---- Update UI ----
    private void UpdateDevUIStatus(SysData data)
    {
        try
        {
            GetDataArry.Md = data.Md;
            GetDataArry.ISSID = data.ISSID;
            GetDataArry.ISec = data.ISec;
            GetDataArry.IKey = data.IKey;
            GetDataArry.ISSID2 = data.ISSID2;
            GetDataArry.ISec2 = data.ISec2;
            GetDataArry.IKey2 = data.IKey2;
            GetDataArry.ASSID = data.ASSID;
            GetDataArry.AHid = data.AHid;
            GetDataArry.ACnty = data.ACnty;
            GetDataArry.ACh = data.ACh;
            GetDataArry.ASec = data.ASec;
            GetDataArry.AKey = data.AKey;
            GetDataArry.DHCP = data.DHCP;
            GetDataArry.IP = data.IP;
            GetDataArry.Msk = data.Msk;
            GetDataArry.GW = data.GW;
        }
        catch (Exception e)
        {
            //OnGetDevHttpRequestError(e);
        }
        finally
        {            
            System.GC.Collect();
        }
    }
    #endregion

    public class SysData
    {
        public int Md { get; set; }
        public string ISSID { get; set; }
        public int ISec { get; set; }
        public string IKey { get; set; }
        public string ISSID2 { get; set; }
        public int ISec2 { get; set; }
        public string IKey2 { get; set; }
        public string ASSID { get; set; }
        public int AHid { get; set; }
        public int ACnty { get; set; }
        public int ACh { get; set; }
        public int ASec { get; set; }
        public string AKey { get; set; }
        public string MAC { get; set; }
        public string AIP { get; set; }
        public string AMsk { get; set; }
        public string AGW { get; set; }
        public int DHCP { get; set; }
        public string IP { get; set; }
        public string Msk { get; set; }
        public string GW { get; set; }
    }

    public class SysChgData
    {
        public int Md { get; set; }
        public string ISSID { get; set; }
        public int ISec { get; set; }
        public string IKey { get; set; }
        public string ISSID2 { get; set; }
        public int ISec2 { get; set; }
        public string IKey2 { get; set; }
        public string ASSID { get; set; }
        public int AHid { get; set; }
        public int ACnty { get; set; }
        public int ACh { get; set; }
        public int ASec { get; set; }
        public string AKey { get; set; }
        public int DHCP { get; set; }
        public string IP { get; set; }
        public string Msk { get; set; }
        public string GW { get; set; }
    }    

    public enum ServiceAction
    {
        Idel = 0,
        GetNetConfig = 1,
        PatchSysInfo = 2,
        GetNetConfig_ag = 3,
        Verify = 4,

        Done = 99,
    }
}

