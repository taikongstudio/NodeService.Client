using System.Runtime.InteropServices;

namespace LANDCexSharp
{
    public unsafe static class NativeMethods
    {
        /// <summary>
        /// 【描述】验证 DLL 的接口版本的兼容性。强烈建议首先验证 DLL 的接口版本！
        ///【参数】dwVerRequested: 指定程序需要的 DLL 接口版本。可用的版本号参见头文件 CexConst.h。
        ///目前最新版本为 LANDCEX_DLL_VER_0_3_1_3，即 0x00030102（VB 中为 &H30103）。
        ///【返值】false，不兼容；true，兼容。

        /// </summary>
        /// <param name="dwVerRequested"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool CheckDllVersion(int dwVerRequested);

        /// <summary>
        /// 【描述】获取 DLL 的接口版本号。推荐使用 CheckDllVersion()而不是直接使用 GetDllVersion()，更容易保
        ///持用户端二次开发代码的兼容性。
        /// </summary>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetDllVersion();

        /// <summary>
        /// 【描述】该函数为升级预留，暂时可以忽略它。
        /// </summary>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetDllProperty();

        /// <summary>
        /// 【描述】将输出的日期数据设置为（或取消）美式习惯。默认的日期格式为“年/月/日”顺序，而美式习
        ///惯为“MM/DD/YYYY”顺序。
        ///【参数】bEnable: true 表示启用，false 表示取消。
        ///【返值】返回本函数调用前的状态，即原来是否启用。

        /// </summary>
        /// <param name="bEnable"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool EnableUSDateFormat(bool bEnable = true);

        /// <summary>
        /// 【描述】通过工作模式 ID 获取其名称。
        ///【参数】cMode: 工作模式 ID。其常用的取值参见头文件 CexConst.h；
        ///VDesc: 获取的工作模式名称，标记为 VT_BSTR 类型的字符串。
        ///【返值】false，失败；true，成功。
        /// </summary>
        /// <param name="cMode"></param>
        /// <param name="vDesc"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetDescriptionOfMode(byte cMode, out Variant vDesc);

        /// <summary>
        /// 【描述】将测试数据文件的概要信息调入内存。只有成功调入后，才能访问其概要信息的具体内容。
        ///概要信息访问完毕，必须调用 FreeBriefInfo 释放内存。
        ///【参数】pszPathName: 指定数据文件，可以包含路径，其扩展名通常为“cex”。
        ///【返值】非 0 成功；0 失败（数据文件未找到，或文件格式、版本不对，或者数据来自非法设备）。
        ///【提示】调入文件时，接口函数将首先检查该测试数据是否来自合法设备，所以您需要同时将设备数据
        ///库文件 LANHE.sys 与 LANDCex.dll 放在一个目录下。
        /// </summary>
        /// <param name="pszPath"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern IntPtr LoadBriefInfo(string pszPath);

        /// <summary>
        /// 【描述】释放由 LoadBriefInfo 调入内存中的概要信息。
        /// </summary>
        /// <param name="hBriefInfo"></param>
        [DllImport("LANDCex_x64.dll")]
        public static extern void FreeBriefInfo(IntPtr hBriefInfo);

        /// <summary>
        /// 【描述】获取概要信息中的测试通道信息。
        ///【参数】hBriefInfoOrDataObj: 可以是由 LoadBriefInfo 调入的仅拥有概要信息的对象，
        ///也可以是由 LoadData 调入的拥有详细数据的对象；
        ///cBoxNo: 获取的设备的箱号；
        ///cChl: 获取的通道编号；
        ///dwDownId: 获取的下位机 ID；
        ///wDownVer: 获取的下位机版本号。
        ///【返值】false，失败；true，成功。

        /// </summary>
        /// <param name="hBriefInfoOrDataObj"></param>
        /// <param name="cBoxNo"></param>
        /// <param name="cChl"></param>
        /// <param name="dwDownId"></param>
        /// <param name="wDownVer"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetChannel(IntPtr hBriefInfoOrDataObj, out byte cBoxNo, out byte cChl, out int dwDownId, out short wDownVer);

        /// <summary>
        /// 【描述】获取电池编号（字符串）。
        ///【参数】hBriefInfoOrDataObj: 由 LoadBriefInfo 调入的概要信息对象，或由 LoadData 调入的详细数据对
        ///象；
        ///vBattNo：获取的电池编号，标记为 VT_BSTR 类型的字符串。
        ///【返值】false，失败；true，成功。

        /// </summary>
        /// <param name="hBriefInfoOrDataObj"></param>
        /// <param name="vBattNo"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetBattNo(IntPtr hBriefInfoOrDataObj, out Variant vDesc);

        /// <summary>
        /// 【描述】获取测试开始时间。
        ///【参数】hBriefInfoOrDataObj: 由 LoadBriefInfo 调入的概要信息对象，或由 LoadData 调入的详细数据对
        ///象。
        ///【返值】返值实质上为 long 型：表示自 1970/01/01 00:00:00 开始计时，已经过去的秒数。

        /// </summary>
        /// <param name="hBriefInfoOrDataObj"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern long GetStartTime(IntPtr hBriefInfoOrDataObj);

        /// <summary>
        /// 【描述】获取测试结束时间。
        ///【参数】hBriefInfoOrDataObj: 由 LoadBriefInfo 调入的概要信息对象，或由 LoadData 调入的详细数据对
        ///象。
        ///【返值】与 GetStartTime 类似。
        /// </summary>
        /// <param name="hBriefInfoOrDataObj"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern long GetEndTime(IntPtr hBriefInfoOrDataObj);

        /// <summary>
        /// 【描述】判断是否为高精度时间。
        ///【参数】hBriefInfoOrDataObj: 由 LoadBriefInfo 调入的概要信息对象，或由 LoadData 调入的详细数据对
        ///象；
        ///【返值】false，表示不是高精度时间；true，表示是高精度时间。
        /// </summary>
        /// <param name="hBriefInfoOrDataObj"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool IsTimeHighResolution(IntPtr hBriefInfoOrDataObj);

        /// <summary>
        /// 【描述】获取化成名（字符串）。
        ///【参数】hBriefInfoOrDataObj: 由 LoadBriefInfo 调入的概要信息对象，或由 LoadData 调入的详细数据对
        ///象；
        ///vName: 获取的化成名，标记为 VT_BSTR 类型的字符串。
        ///【返值】false，失败；true，成功。
        /// </summary>
        /// <param name="hBriefInfoOrDataObj"></param>
        /// <param name="vName"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetFormationName(IntPtr hBriefInfoOrDataObj, out Variant vDesc);

        /// <summary>
        /// 【描述】将测试数据文件的详细数据调入内存。只有成功调入后，才能访问其概要信息和详细数据。
        ///数据访问完毕，必须调用 FreeData 释放内存。
        ///【参数】pszPath: 指定数据文件(.cex)，可以包含路径；
        ///nHopeUnitScheme: 指定单位方案，其取值参见头文件 CexConst.h。
        ///【返值】非 0 成功；0 失败（数据文件未找到，或文件格式、版本不对，或者数据来自非法设备）。
        ///【提示】调入文件时，接口函数将首先检查该测试数据是否来自合法设备，所以您需要同时将设备数据
        ///库文件 LANHE.sys 与 LANDCex.dll 放在一个目录下
        /// </summary>
        /// <param name="pszPath"></param>
        /// <param name="nHopeUnitScheme"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern IntPtr LoadData(string pszPath, int nHopeUnitScheme);

        /// <summary>
        /// 【描述】释放由 LoadData 调入内存中的详细数据。
        /// </summary>
        /// <param name="hDataObj"></param>
        [DllImport("LANDCex_x64.dll")]
        public static extern void FreeData(IntPtr hDataObj);

        /// <summary>
        /// 【描述】获取指定数据列的行数，即数据的个数。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据对象；
        ///columnID: 指定数据的列 ID，其取值参见头文件 CexConst.h。
        ///【返值】>0 成功；<=0 失败（入口参数无效）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="columnID"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetRows(IntPtr hDataObj, uint columnID);

        /// <summary>
        /// 【描述】与上文的 GetRows(…) 完全对应。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="pszColIdStr"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetRows2(IntPtr hDataObj, string pszColIdStr);

        /// <summary>
        /// 【描述】获取指定位置的数据。
        ///    【参数】hDataObj: 由 LoadData 调入的详细数据对象；
        ///columnID: 指定数据的列 ID，其取值参见头文件 CexConst.h；
        ///nRow: 指定数据的行序号；
        ///vData: 返回指定位置的数据。
        ///    【返值】false，失败；true，成功。
        ///     【提示】当返回成功时，返值名义上为 VARIANT 类型，它通常可以精确地转换为一个明确的数据类型。
        ///其具体情况，视传入的参数 columnID 而定。
        ///绝大多数的情况下，可以精确转换为一个 float 类型（即 VT_R4 型）。以下是特殊情况：
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="columnID"></param>
        /// <param name="nRow"></param>
        /// <param name="vData"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetDataEx(IntPtr hDataObj, uint columnID, int nRow,  Variant* vData);

        /// <summary>
        /// 【描述】与上文的 GetDataEx(…) 完全对应，仅仅是为了程序的易读。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="pszColIdStr"></param>
        /// <param name="nRow"></param>
        /// <param name="vData"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetDataEx2(IntPtr hDataObj, string pszColIdStr, int nRow, out Variant vData);

        /// <summary>
        /// 【描述】该函数是上文 GetDataEx(…) 的特殊情况，只能用于访问 float 类型（即 VT_R4 型）的数据，如
        ///电压、电流等等。【返值】返回 float 型的数据。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="columnID"></param>
        /// <param name="nRow"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern float GetDataAsFloat(IntPtr hDataObj, uint columnID, int nRow);

        /// <summary>
        /// 【描述】该函数是上文 GetDataEx(…) 的特殊应用，只能用于访问 BYTE 型（即 VT_UI1 型）的数据，如
        ///工步模式。
        ///【返值】返回 BYTE 型的数据。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="columnID"></param>
        /// <param name="nRow"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern byte GetDataAsByte(IntPtr hDataObj, uint columnID, int nRow);

        /// <summary>
        /// 【描述】与上文的 GetDataEx (…) 功能完全等效。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="columnID"></param>
        /// <param name="nRow"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern IntPtr GetData(IntPtr hDataObj, uint columnID, int nRow);

        /// <summary>
        /// 【描述】从字符串化的列 Id 获取其对应的数字化的列 ID。
        ///【返值】返回 0xffffffff，失败；反之成功。
        /// </summary>
        /// <param name="pszColIdStr"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern uint Str_to_ColumnID(string pszColIdStr);

        /// <summary>
        /// 【描述】从数字化的列 ID 获取其对应的字符串化的列 Id。
        ///【返值】false，失败；true，成功。
        /// </summary>
        /// <param name="columnID"></param>
        /// <param name="vColIdStr"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool ColumnID_to_Str(uint columnID, IntPtr vColIdStr);

        /// <summary>
        /// 【描述】获取“列”ID 的文字描述。
        ///【参数】columnID: 指定数据的列 ID，其取值参见头文件 CexConst.h；
        ///VDesc: 返回指定数据列 ID 对应的文字描述，标记为 VT_BSTR 类型的字符串。
        ///0x2002 "Step.Mode" BYTE (即 VT_UI1 型)
        ///BYTE cMode = (BYTE)(_variant_t)GetData(…);
        ///以下是几个返值的常数定义（参见头文件 CexConst.h）：
        ///#define PM_CONST_CURRENT_DISCHARGE 0x02
        ///#define PM_CONST_CURRENT_CHARGE 0x03
        ///……
        ///另：可继续通过 GetDescriptionOfMode 函数获取文本
        ///名称。
        ///0x400b "Rec.SysTime" BSTR (即 VT_BSTR 型) CString strSysTime = (LPCSTR)(_variant_t)GetData(…);
        ///4) bool __stdcall GetDataEx2(HANDLE hDataObj, const char* pszColIdStr, int nRow, VARIANT& vData)
        ///【描述】与上文的 GetDataEx(…) 完全对应，仅仅是为了程序的易读。
        ///9 / 13
        ///【返值】false，失败；true，成功。
        /// </summary>
        /// <param name="columnID"></param>
        /// <param name="vDesc"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetDescriptionOfColumn(uint columnID, out Variant vDesc);

        /// <summary>
        /// 【描述】获取当前数据文件指定子表列的物理单位名称。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        /// columnID: 指定数据的列 ID，其取值参见头文件 CexConst.h；
        ///vName: 返回当前数据文件指定子表列的物理单位名称。
        ///【返值】false，失败；true，成功。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="columnID"></param>
        /// <param name="vName"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetUnitNameOfColumn(IntPtr hDataObj, uint columnID, out Variant vDesc);

        /// <summary>
        /// 【描述】获取当前记录的循环序号和工步序号。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nRec: 指定记录序号；
        ///   pnStep: 指定工步序号的返回地址。
        ///【返值】>=0 成功；<0 失败（入口参数无效）。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nRec"></param>
        /// <param name="pnStep"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetCycleFromRec(IntPtr hDataObj, int nRec, IntPtr pnStep = 0);

        /// <summary>
        /// 【描述】获取循环第一条记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstRecOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环最后一条记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效）。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastRecOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环第一条充电记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有充电数据）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstChargeRecOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环最后一条充电记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///nCycle: 指定循环序号。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastChargeRecOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环第一条放电记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///    nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有放电数据）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstDischRecOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环最后一条放电记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有放电数据）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>

        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastDischRecOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        ///        【描述】获取工步第一条记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nStep: 指定工步序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该工步没有记录数据）。
        /// <param name="hDataObj"></param>
        /// <param name="nStep"></param>
        /// <returns></returns>


        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstRecOfStep(IntPtr hDataObj, int nStep);

        /// <summary>
        /// 【描述】获取工步最后一条记录。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///        nStep: 指定工步序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该工步没有记录数据）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nStep"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastRecOfStep(IntPtr hDataObj, int nStep);

        /// <summary>
        /// 【描述】获取循环第一个测试工步。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstStepOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环最后一个测试工步。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效）。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastStepOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环第一个充电测试工步。
        /// 【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///         nCycle: 指定循环序号。
        /// 【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有充电数据）。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstChargeStepOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环最后一个充电测试工步。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///        nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有充电数据）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastChargeStepOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环第一个放电测试工步。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///       nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有放电数据）。
        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetFirstDischStepOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取循环最后一个放电测试工步。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///        nCycle: 指定循环序号。
        ///【返值】>=0 成功；<0 失败（入口参数无效，或该循环没有放电数据）。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetLastDischStepOfCycle(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取测试工步流程的个数（一次“工步参数重置”操作，会增加一个工步流程）。
        ///   【参数】hDataObj: 由 LoadData 调入的详细数据。
        ///【返值】>0 成功，即返回个数；<=0 失败（其中<0 表示入口参数无效）。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="nCycle"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetNumOfProcedure(IntPtr hDataObj, int nCycle);

        /// <summary>
        /// 【描述】获取工步流程名（字符串）及启动时间。
        ///【参数】hDataObj: 由 LoadData 调入的详细数据；
        ///VName: 获取的工步流程名；
        ///nIndex: 指定工步流程的序号；
        ///pnHappenTime: 获取的工步流程对应的启动时间（可选；格式与 GetStartTime 相同）。
        ///【返值】false，失败；true，成功。

        /// </summary>
        /// <param name="hDataObj"></param>
        /// <param name="vName"></param>
        /// <param name="nIndex"></param>
        /// <param name="pnHappenTime"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]
        public static extern int GetProcedureName(IntPtr hDataObj, Variant* vName, int nIndex = -1, IntPtr pnHappenTime = 0);


        /// <summary>
        /// 【描述】通过箱号和通道号得到通道数据的全路径。
        ///【参数】cBoxNo : 指定箱号；
        /// cChl: 指定通道号；
        /// vPath: 获取指定箱号和通道号的通道数据的全路径。
        ///【返值】false，失败；true，成功。

        /// </summary>
        /// <param name="cBoxNo"></param>
        /// <param name="cChl"></param>
        /// <param name="vPath"></param>
        /// <returns></returns>

        [DllImport("LANDCex_x64.dll")]
        public static extern bool GetChlDataFullPath(byte cBoxNo, byte cChl, Variant* vDesc);


        /// <summary>
        ///     【描述】调入通道快照信息。通道快照信息就是通道数据在某一时刻的状况，它是指向保存在存储设备
        ///中的通道数据的引用标记或指针。只有成功调入，且未释放（FreeChlSnapshot），才能访问通
        ///道快照信息。通道快照信息访问完毕，必须调用 FreeChlSnapshot 释放内存。
        ///【参数】pnChlCount : 返回通道总数；
        ///nDirection : 指定通道快照方式，其取值参见头文件 CexConst.h。
        ///    【返值】返回非 0，成功；false ，失败。
        /// </summary>
        /// <param name="pnChlCount"></param>
        /// <param name="nDirection"></param>
        /// <returns></returns>

        [DllImport("LANDCex_x64.dll")]
        public static extern IntPtr LoadChlSnapshot(uint* pnChlCount, int nDirection);

        /// <summary>
        /// 【描述】释放由 LoadChlSnapshot 调入内存中的通道快照信息。【参数】hChlSnapshot: 通道快照信息对象。
        /// </summary>
        /// <param name="hChlSnapshot"></param>
        [DllImport("LANDCex_x64.dll")]
        public static extern void FreeChlSnapshot(IntPtr hChlSnapshot);


        /// <summary>
        /// 【描述】从通道快照中获取箱号和通道号。
        ///【参数】hChlSnapshot: 由 LoadChlSnapshot 调入的通道快照信息的对象；
        ///nIndex: 指定通道索引；
        ///cBoxNo : 返回的箱号；
        ///cChl: 返回通道号。
        ///【返值】true，成功；false， 失败。
        /// </summary>
        /// <param name="hChlSnapshot"></param>
        /// <param name="nIndex"></param>
        /// <param name="cBoxNo"></param>
        /// <param name="cChl"></param>
        /// <returns></returns>
        [DllImport("LANDCex_x64.dll")]

        public static extern bool ChannelFromChlSnapshot(IntPtr hChlSnapshot, int nIndex, byte* cBoxNo, byte* cChl);

    }
}
