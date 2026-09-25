//
// MicroGate Serial API
//
// MicroGate and SyncLink are trademarks of MicroGate Systems, Ltd
// Copyright (C) MicroGate Systems, Ltd 1997-2025
//

#ifndef _MGHDLC_H_
#define _MGHDLC_H_

#pragma pack(8)

#define MGSL_MAX_PORTS			200
#define HDLC_MAX_FRAME_SIZE	65535
#define MAX_ASYNC_TRANSMIT		4096

#define MGSL_MAKE_PORT_ID(AdapterNumber,PortNumber) ( (AdapterNumber) + ((PortNumber) << 16) )
#define MGSL_GET_ADAPTER(PortID) ((PortID) & 0xffff)
#define MGSL_GET_PORT(PortID) ((PortID) >> 16)

#ifndef UINT
typedef unsigned int UINT;
#endif

/*
** Trace event structure
**
** Time Stamp Format
**
** Bits <32..27>	Hour				5 Bits, range 0-23
** Bits <26..21>	Minutes			6 Bits, range 0-59
** Bits <20..15>	Seconds			6 Bits, range 0-59
** Bits <09..00>	Milliseconds	10 bits, range 0-999
*/

#pragma pack(1)
typedef struct _MGSL_TRACE_EVENT_HEADER
{
	ULONG TimeStamp;
	ULONG EventType;
	USHORT DataLength;

} MGSL_TRACE_EVENT_HEADER, *PMGSL_TRACE_EVENT_HEADER;
#pragma pack(8)

typedef struct _MGSL_TRACE_EVENT
{
	ULONG TimeStamp;
	ULONG EventType;
	USHORT DataLength;
	char EventData[HDLC_MAX_FRAME_SIZE];

} MGSL_TRACE_EVENT, *PMGSL_TRACE_EVENT;

/*
** Trace levels for use with MgslSetTraceLevel
*/

#define TraceLevel_None      0x0000
#define TraceLevel_API       0x0001
#define TraceLevel_Status    0x0002
#define TraceLevel_Transmit  0x0004
#define TraceLevel_Receive   0x0008
#define TraceLevel_Raw       0x0010
#define TraceLevel_Data      0x0020
#define TraceLevel_DataLink  0x0040
#define TraceLevel_Error     0x0100
#define TraceLevel_Info      0x0200
#define TraceLevel_Detail    0x0400

#define EventType_None					0

#define TransmitEvent_Enable			1
#define TransmitEvent_Disable			2
#define TransmitEvent_Start			3
#define TransmitEvent_StatusIrq		4
#define TransmitEvent_DataIrq			5
#define TransmitEvent_DmaIrq			6

#define ReceiveEvent_Enable			7
#define ReceiveEvent_Disable			8
#define ReceiveEvent_StatusIrq		9
#define ReceiveEvent_DataIrq			10
#define ReceiveEvent_DmaIrq			11

#define StatusEvent_StatusChange		12

#define ApiEvent_SetParamsCall  				13
#define ApiEvent_SetParamsReturn				14
#define ApiEvent_GetParamsCall				15
#define ApiEvent_GetParamsReturn				16
#define ApiEvent_SetSerialSignalsCall		17
#define ApiEvent_SetSerialSignalsReturn	18
#define ApiEvent_GetSerialSignalsCall		19
#define ApiEvent_GetSerialSignalsReturn	20
#define ApiEvent_WaitEventCall				21
#define ApiEvent_WaitEventReturn				22
#define ApiEvent_WaitEventComplete			23
#define ApiEvent_CancelWaitEventCall		24
#define ApiEvent_CancelWaitEventReturn		25
#define ApiEvent_EnableTransmitterCall		26
#define ApiEvent_EnableTransmitterReturn	27
#define ApiEvent_TransmitCall					28
#define ApiEvent_TransmitReturn				29
#define ApiEvent_TransmitComplete			30
#define ApiEvent_CancelTransmitCall			31
#define ApiEvent_CancelTransmitReturn		32
#define ApiEvent_SetIdleModeCall				33
#define ApiEvent_SetIdleModeReturn			34
#define ApiEvent_EnableReceiverCall			35
#define ApiEvent_EnableReceiverReturn		36
#define ApiEvent_ReceiveCall					37
#define ApiEvent_ReceiveReturn				38
#define ApiEvent_ReceiveComplete				39
#define ApiEvent_CancelReceiveCall			40
#define ApiEvent_CancelReceiveReturn		41
#define ApiEvent_ResetTraceBuffersCall		42
#define ApiEvent_ResetTraceBuffersReturn	43
#define ApiEvent_SetTraceLevelCall			44
#define ApiEvent_SetTraceLevelReturn		45
#define ApiEvent_GetTraceLevelCall			46
#define ApiEvent_GetTraceLevelReturn		47

#define DataEvent_TransmitData		48
#define DataEvent_ReceiveData			49

#define EventType_BufferOverflow		50

#define EventType_FormatRaw			51

#define ApiEvent_GetAssignedResourcesCall		52
#define ApiEvent_GetAssignedResourcesReturn	53

#define ApiEvent_LoopModeSendDoneCall			54
#define ApiEvent_LoopModeSendDoneReturn		55

#define EventType_DLEvent							56
#define EventType_DLState							57
#define EventType_DLFrame							58
#define EventType_DLTxActive						59
#define EventType_DLTxIdle							60
#define EventType_DLTxLoopComplete				61
#define EventType_DLGaLoopComplete				62
#define EventType_DLTxTimeout						63

#define ApiEvent_DlAllocateCall					64
#define ApiEvent_DlAllocateReturn				65
#define ApiEvent_DlCallCall						66
#define ApiEvent_DlCallReturn						67
#define ApiEvent_DlCancelWaitCall				68
#define ApiEvent_DlCancelWaitReturn				69
#define ApiEvent_DlFreeCall						70
#define ApiEvent_DlFreeReturn						71
#define ApiEvent_DlGetParamsCall					72
#define ApiEvent_DlGetParamsReturn				73
#define ApiEvent_DlGetPhysParamsCall			74
#define ApiEvent_DlGetPhysParamsReturn			75
#define ApiEvent_DlGetStatsCall					76
#define ApiEvent_DlGetStatsReturn				77
#define ApiEvent_DlResetStatsCall				78
#define ApiEvent_DlResetStatsReturn				79
#define ApiEvent_DlSetParamsCall					80
#define ApiEvent_DlSetParamsReturn				81
#define ApiEvent_DlSetPhysParamsCall			82
#define ApiEvent_DlSetPhysParamsReturn			83
#define ApiEvent_DlWaitCall						84
#define ApiEvent_DlWaitComplete					85
#define ApiEvent_DlWaitReturn						86

#define ApiEvent_DiSetParamsCall  				87
#define ApiEvent_DiSetParamsReturn				88
#define ApiEvent_DiGetParamsCall					89
#define ApiEvent_DiGetParamsReturn				90
#define ApiEvent_DiSetSerialSignalsCall		91
#define ApiEvent_DiSetSerialSignalsReturn		92
#define ApiEvent_DiGetSerialSignalsCall		93
#define ApiEvent_DiGetSerialSignalsReturn		94
#define ApiEvent_DiMonitorEventsCall			95
#define ApiEvent_DiMonitorEventsReturn			96
#define ApiEvent_DiEventReady						97
#define ApiEvent_DiEnableTransmitterCall		98
#define ApiEvent_DiEnableTransmitterReturn	99
#define ApiEvent_DiTransmitCall					100
#define ApiEvent_DiTransmitReturn				101
#define ApiEvent_DiTransmitComplete				102
#define ApiEvent_DiCancelTransmitCall			103
#define ApiEvent_DiCancelTransmitReturn		104
#define ApiEvent_DiSetIdleModeCall				105
#define ApiEvent_DiSetIdleModeReturn			106
#define ApiEvent_DiEnableReceiverCall			107
#define ApiEvent_DiEnableReceiverReturn		108
#define ApiEvent_DiReceiveCall					109
#define ApiEvent_DiReceiveReturn					110
#define ApiEvent_DiReceiveReady					111
#define ApiEvent_DiResetTraceBuffersCall		112
#define ApiEvent_DiResetTraceBuffersReturn	113
#define ApiEvent_DiSetTraceLevelCall			114
#define ApiEvent_DiSetTraceLevelReturn			115
#define ApiEvent_DiGetTraceLevelCall			116
#define ApiEvent_DiGetTraceLevelReturn			117
#define ApiEvent_DiLoopModeSendDoneCall		118
#define ApiEvent_DiLoopModeSendDoneReturn		119

#define ApiEvent_DiDlAllocateCall				120
#define ApiEvent_DiDlAllocateReturn				121
#define ApiEvent_DiDlCallCall						122
#define ApiEvent_DiDlCallReturn					123
#define ApiEvent_DiDlFreeCall						124
#define ApiEvent_DiDlFreeReturn					125
#define ApiEvent_DiDlGetParamsCall				126
#define ApiEvent_DiDlGetParamsReturn			127
#define ApiEvent_DiDlGetPhysParamsCall			128
#define ApiEvent_DiDlGetPhysParamsReturn		129
#define ApiEvent_DiDlGetStatsCall				130
#define ApiEvent_DiDlGetStatsReturn				131
#define ApiEvent_DiDlResetStatsCall				132
#define ApiEvent_DiDlResetStatsReturn			133
#define ApiEvent_DiDlSetParamsCall				134
#define ApiEvent_DiDlSetParamsReturn			135
#define ApiEvent_DiDlSetPhysParamsCall			136
#define ApiEvent_DiDlSetPhysParamsReturn		137
#define ApiEvent_DiDlWaitCall						138
#define ApiEvent_DiDlWaitReturn					139
#define ApiEvent_DiDlReady							140

#define ApiEvent_DlSetAddrListCall              141
#define ApiEvent_DlSetAddrListReturn            142
#define ApiEvent_DiDlSetAddrListCall            143
#define ApiEvent_DiDlSetAddrListReturn          144

#define ApiEvent_SetOptionCall                  145
#define ApiEvent_SetOptionReturn                146
#define ApiEvent_GetOptionCall                  147
#define ApiEvent_GetOptionReturn                148
#define ApiEvent_DiSetOptionCall                149
#define ApiEvent_DiSetOptionReturn              150
#define ApiEvent_DiGetOptionCall                151
#define ApiEvent_DiGetOptionReturn              152

#define ApiEvent_SetGpioCall                    153
#define ApiEvent_SetGpioReturn                  154
#define ApiEvent_GetGpioCall                    155
#define ApiEvent_GetGpioReturn                  156
#define ApiEvent_WaitGpioCall                   157
#define ApiEvent_WaitGpioReturn                 158
#define ApiEvent_WaitGpioComplete               159
#define ApiEvent_CancelWaitGpioCall             160
#define ApiEvent_CancelWaitGpioReturn           161

#define ApiEvent_ReadCall						162
#define ApiEvent_ReadReturn						163
#define ApiEvent_WriteCall						164
#define ApiEvent_WriteReturn					165
#define ApiEvent_WaitAllSentCall				166
#define ApiEvent_WaitAllSentReturn				167
#define ApiEvent_ReadWithStatusCall				168
#define ApiEvent_ReadWithStatusReturn			169

#define EventType_UserBase				0x80000000
#define EventType_UserMessage			EventType_UserBase + 1

#define ASYNC_PARITY_NONE				0
#define ASYNC_PARITY_EVEN				1
#define ASYNC_PARITY_ODD				2

#define HDLC_FLAG_UNDERRUN_ABORT7	0x0000
#define HDLC_FLAG_UNDERRUN_ABORT15	0x0001
#define HDLC_FLAG_UNDERRUN_FLAG		0x0002
#define HDLC_FLAG_UNDERRUN_CRC		0x0004
#define HDLC_FLAG_SHARE_ZERO			0x0010
#define HDLC_FLAG_AUTO_CTS				0x0020
#define HDLC_FLAG_AUTO_DCD				0x0040
#define HDLC_FLAG_AUTO_RTS				0x0080
#define HDLC_FLAG_RXC_DPLL				0x0100
#define HDLC_FLAG_RXC_BRG				0x0200
#define HDLC_FLAG_RXC_TXCPIN			0x8000
#define HDLC_FLAG_RXC_RXCPIN			0x0000
#define HDLC_FLAG_TXC_DPLL				0x0400
#define HDLC_FLAG_TXC_BRG				0x0800
#define HDLC_FLAG_TXC_TXCPIN			0x0000
#define HDLC_FLAG_TXC_RXCPIN			0x0008
#define HDLC_FLAG_DPLL_DIV8			0x1000
#define HDLC_FLAG_DPLL_DIV16			0x2000
#define HDLC_FLAG_DPLL_DIV32			0x0000
#define HDLC_FLAG_HDLC_LOOPMODE		0x4000
#define HDLC_FLAG_RXC_INV           0x0002
#define HDLC_FLAG_TXC_INV           0x0004

#define HDLC_CRC_NONE					0
#define HDLC_CRC_16_CCITT				1
#define HDLC_CRC_32_CCITT				2
#define HDLC_CRC_MODE					0x00ff
#define HDLC_CRC_RETURN_CRCERR_FRAME    0x8000
#define HDLC_CRC_RETURN_CRC             0x4000

#define HDLC_TXIDLE_FLAGS				0
#define HDLC_TXIDLE_ALT_ZEROS_ONES	1
#define HDLC_TXIDLE_ZEROS				2
#define HDLC_TXIDLE_ONES				3
#define HDLC_TXIDLE_ALT_MARK_SPACE	4
#define HDLC_TXIDLE_SPACE				5
#define HDLC_TXIDLE_MARK				6
#define HDLC_TXIDLE_CUSTOM_8            0x10000000
#define HDLC_TXIDLE_CUSTOM_16           0x20000000

#define HDLC_ENCODING_NRZ						0
#define HDLC_ENCODING_NRZB						1
#define HDLC_ENCODING_NRZI_MARK				2
#define HDLC_ENCODING_NRZI_SPACE				3
#define HDLC_ENCODING_NRZI						HDLC_ENCODING_NRZI_SPACE
#define HDLC_ENCODING_BIPHASE_MARK			4
#define HDLC_ENCODING_BIPHASE_SPACE			5
#define HDLC_ENCODING_BIPHASE_LEVEL			6
#define HDLC_ENCODING_DIFF_BIPHASE_LEVEL	7

#define HDLC_PREAMBLE_LENGTH_8BITS			0
#define HDLC_PREAMBLE_LENGTH_16BITS			1
#define HDLC_PREAMBLE_LENGTH_32BITS			2
#define HDLC_PREAMBLE_LENGTH_64BITS			3

#define HDLC_PREAMBLE_PATTERN_NONE			0
#define HDLC_PREAMBLE_PATTERN_ZEROS			1
#define HDLC_PREAMBLE_PATTERN_FLAGS			2
#define HDLC_PREAMBLE_PATTERN_10				3
#define HDLC_PREAMBLE_PATTERN_01				4
#define HDLC_PREAMBLE_PATTERN_ONES			5

#define MGSL_MODE_ASYNC			1
#define MGSL_MODE_HDLC			2
#define MGSL_MODE_MONOSYNC      3
#define MGSL_MODE_BISYNC        4
#define MGSL_MODE_EXTERNALSYNC	6
#define MGSL_MODE_RAW			MGSL_MODE_EXTERNALSYNC
#define MGSL_MODE_TDM           7
#define MGSL_MODE_XSYNC         8

#define MGSL_BUS_TYPE_ISA			1
#define MGSL_BUS_TYPE_EISA			2
#define MGSL_BUS_TYPE_PCI			5
#define MGSL_BUS_TYPE_PCMCIA		8

typedef struct _MGSL_PARAMS
{
	/* Common */

	ULONG		Mode;				/* Asynchronous or HDLC */
	UCHAR		Loopback;		/* internal loopback mode */

	/* HDLC Only */

	USHORT	Flags;
	UCHAR		Encoding;		/* NRZ, NRZI, etc. */
	ULONG		ClockSpeed;		/* external clock speed in bits per second */
	UCHAR		Addr;				/* receive HDLC address filter, 0xFF = disable */
	USHORT	CrcType;			/* HDLC_CRC_XXX (none/16/32) */
	UCHAR		PreambleLength;
	UCHAR		PreamblePattern;

	/* Async Only */

	ULONG		DataRate;		/* bits per second */
	UCHAR		DataBits;		/* 7 or 8 data bits */
	UCHAR		StopBits;		/* 1 or 2 stop bits */
	UCHAR		Parity;			/* none, even, or odd */

} MGSL_PARAMS, *PMGSL_PARAMS;

// Original port configuration structure is
// deprecated, used MGSL_PORT_CONFIG_EX
typedef struct _MGSL_PORT_CONFIG
{
	ULONG BaseAddress;
	ULONG IrqLevel;
	ULONG DmaChannel;
	ULONG BusType;
	ULONG BusNumber;

} MGSL_PORT_CONFIG, *PMGSL_PORT_CONFIG;

typedef struct _MGSL_PORT_CONFIG_EX
{
	ULONG Size;				// size of structure in bytes
	ULONG BaseAddress;	// NT4 ISA only
	ULONG IrqLevel;		// NT4 ISA only
	ULONG DmaChannel;		// NT4 ISA only
	ULONG BusType;
	ULONG BusNumber;
	ULONG DeviceID;
	ULONG MaxFrameSize;
	ULONG Flags;

} MGSL_PORT_CONFIG_EX, *PMGSL_PORT_CONFIG_EX;

/* used in Flags member of MGSL_PORT_CONFIG_EX structure */

#define MGSL_INTERFACE_MASK    0xf
#define MGSL_INTERFACE_DISABLE 0
#define MGSL_INTERFACE_RS232   1
#define MGSL_INTERFACE_V35     2
#define MGSL_INTERFACE_RS422   3
#define MGSL_INTERFACE_RS530A  4
#define MGSL_RTS_DRIVER_CONTROL 0x0010
#define MGSL_NO_TERMINATION    0x0020

#define MICROGATE_VENDOR_ID    0x13c0
#define SYNCLINK_DEVICE_ID     0x0010
#define SYNCLINK_SCC_DEVICE_ID 0x0020
#define SYNCLINK_SCA_DEVICE_ID 0x0030
#define SYNCLINK_T1_DEVICE_ID  0x0040
#define SYNCLINK_PCCARD_DEVICE_ID 0x0050
#define SYNCLINK_MSC_DEVICE_ID 0x0060
#define SYNCLINK_GT_DEVICE_ID  0x0070
#define SYNCLINK_GT4_DEVICE_ID 0x0080
#define SYNCLINK_AC_DEVICE_ID  0x0090
#define SYNCLINK_GT2_DEVICE_ID 0x00A0
#define SYNCLINK_USB_DEVICE_ID 0x00B0
#define MGSL_MAX_SERIAL_NUMBER 30

typedef struct _MGSL_ASSIGNED_RESOURCES
{
	ULONG BusType;
	ULONG BusNumber;
	ULONG DeviceNumber;
	ULONG IrqLevel;
	ULONG DmaChannel;
	ULONG IoAddress1;
	ULONG IoAddress2;
	ULONG IoAddress3;
	ULONG MemAddress1;
	ULONG MemAddress2;
	ULONG MemAddress3;
	USHORT DeviceId;
	USHORT SubsystemId;
	char SerialNumber[MGSL_MAX_SERIAL_NUMBER];

} MGSL_ASSIGNED_RESOURCES, *PMGSL_ASSIGNED_RESOURCES;

#define BUSNUM( a ) ((a) & 0xffff)
#define DEVNUM( a ) ((a) >> 16)
#define BUSDEV( a, b ) ( (ULONG)(a) + (((ULONG)(b)) << 16) )

typedef struct _MGSL_RECEIVE_REQUEST
{
	ULONG Status;			/* returned request status */
	ULONG DataLength;		/* returned data length */
	char DataBuffer[1];	/* variable length data buffer */

} MGSL_RECEIVE_REQUEST, *PMGSL_RECEIVE_REQUEST;

typedef struct _GPIO_DESC
{
	UINT state;
	UINT smask;
	UINT dir;
	UINT dmask;

} GPIO_DESC;

/*
** Option IDs used with MgslSetOption/MgslGetOption
*/
#define MGSL_OPT_RX_DISCARD_TOO_LARGE   1
#define MGSL_OPT_UNDERRUN_RETRY_LIMIT   2
#define MGSL_OPT_ENABLE_LOCALLOOPBACK	3
#define MGSL_OPT_ENABLE_REMOTELOOPBACK	4
#define MGSL_OPT_JCR                    5
#define MGSL_OPT_INTERFACE              6
#define MGSL_OPT_RTS_DRIVER_CONTROL     7
#define MGSL_OPT_RX_ERROR_MASK          8
#define MGSL_OPT_CLOCK_SWITCH           9
#define MGSL_OPT_CLOCK_BASE_FREQ        10
#define MGSL_OPT_HALF_DUPLEX            11
#define MGSL_OPT_MSB_FIRST              12
#define MGSL_OPT_RX_COUNT               13
#define MGSL_OPT_TX_COUNT               14
#define MGSL_OPT_CUSTOM                 15
#define MGSL_OPT_RX_POLL                16
#define MGSL_OPT_TX_POLL                17
#define MGSL_OPT_NO_TERMINATION         18
#define MGSL_OPT_TDM                    19
#define MGSL_OPT_AUXCLK_ENABLE          20
#define MGSL_OPT_UNDERRUN_COUNT         21
#define MGSL_OPT_TX_IDLE_COUNT          22
#define MGSL_OPT_DPLL_RESET             23
#define MGSL_OPT_RS422_OE               24
#define MGSL_OPT_CLOCK_BASE_SOURCE      25
#define MGSL_OPT_USEC_DELAY             26
#define MGSL_OPT_XSYNC                  27
#define MGSL_OPT_XCTRL                  28

// valid values for MGSL_OPT_CLOCK_BASE_SOURCE
#define MGSL_CLOCK_BASE_FIXED           0
#define MGSL_CLOCK_BASE_SYNTH           1

/*
** Returned status values for MgslReceive
*/

#define RxStatus_OK					0
#define RxStatus_CrcError			1
#define RxStatus_FifoOverrun		2
#define RxStatus_ShortFrame		3
#define RxStatus_Abort				4
#define RxStatus_BufferOverrun	5
#define RxStatus_Cancel				6
#define RxStatus_BufferTooSmall	7

/*
** Returned status values for MgslTransmit
*/

#define TxStatus_OK					0
#define TxStatus_Underrun			1
#define TxStatus_Cancel				2
#define TxStatus_CtsFailure		3

/*
** Event bit flags for use with MgslWaitEvent
*/

#define MgslEvent_DsrActive		0x0001
#define MgslEvent_DsrInactive		0x0002
#define MgslEvent_Dsr				0x0003
#define MgslEvent_CtsActive		0x0004
#define MgslEvent_CtsInactive		0x0008
#define MgslEvent_Cts				0x000c
#define MgslEvent_DcdActive		0x0010
#define MgslEvent_DcdInactive		0x0020
#define MgslEvent_Dcd				0x0030
#define MgslEvent_RiActive			0x0040
#define MgslEvent_RiInactive		0x0080
#define MgslEvent_Ri					0x00c0
#define MgslEvent_ExitHuntMode	0x0100
#define MgslEvent_IdleReceived	0x0200

/*
** Return diagnostics status for MgslOpenDiagnostics
*/

#define DiagStatus_OK								0
#define DiagStatus_AddressFailure				1
#define DiagStatus_AddressConflict				2
#define DiagStatus_IrqFailure						3
#define DiagStatus_IrqConflict					4
#define DiagStatus_DmaFailure						5
#define DiagStatus_DmaConflict					6
#define DiagStatus_PciAdapterNotFound			7
#define DiagStatus_CantAssignPciResources		8
#define DiagStatus_CantAssignPciMemAddr		9
#define DiagStatus_CantAssignPciIoAddr			10
#define DiagStatus_CantAssignPciIrq				11
#define DiagStatus_MemoryError					12
#define DiagStatus_MemAllocFailure				13


/* data link layer parameters */
/* used with MgslDlGetParams() and MgslDlSetParams() */

#define MGSL_MAX_ADDRESS	16
typedef struct _MGSL_DLPARAMS
{
	ULONG Link;				/* link instance identifier */

	ULONG	Flags;			/* bitmask of link layer flags */

	UCHAR	LocalAddr[MGSL_MAX_ADDRESS];	/* local address */
	ULONG	LocalAddrLength;

	UCHAR	RemoteAddr[MGSL_MAX_ADDRESS];	/* remote address */
	ULONG	RemoteAddrLength;

	UCHAR	BcastAddr[MGSL_MAX_ADDRESS];	/* broadcast command address */
	ULONG	BcastAddrLength;

	ULONG	TransmitWindow;	/* maximum outstanding I-frames */
	ULONG	RetryLimit;			/* maximum number of retransmissions */
	ULONG	MaxInfoField;		/* max info field size in bytes */

	ULONG	ResponseTimer;		/* transmit/retransmit timer (seconds)*/
	ULONG	IdleTimer;			/* idle timeout (seconds) */

	ULONG	Reserved1;	/* must be zero */
	ULONG	Reserved2;	/* must be zero */

} MGSL_DLPARAMS, *PMGSL_DLPARAMS;

typedef struct _MGSL_DLPHYSPARAMS
{
	ULONG	Flags;				/* bitmask of link layer flags */
	ULONG	TransmitTimeout;	/* transmit timeout (milliseconds) */
	ULONG	TxActiveIdle;		/* idle mode for transmit active */
	USHORT	CtsTransmitDelay;	/* cts transmit delay (milliseconds) */
	USHORT	InterframeDelay;	/* interframe delay (milliseconds) */
	USHORT	RtsDropDelay;		/* rts drop delay (milliseconds) */
	USHORT	TurnaroundDelay;	/* turnaround delay (milliseconds) */

} MGSL_DLPHYSPARAMS, *PMGSL_DLPHYSPARAMS;

/* data link layer primitive */
/* used with MgslDlCall and MgslDlWait */

typedef struct _MGSL_DLPRIMITIVE
{
	ULONG Link;
	ULONG Type;
	ULONG Flags;
	ULONG Size;
	char Buffer[1];
} MGSL_DLPRIMITIVE, *PMGSL_DLPRIMITIVE;

/* used with DLINDICATION_RAWDATA */
typedef struct _MGSL_RAWDATA
{
	UCHAR Address[MGSL_MAX_ADDRESS];
	ULONG AddressLength;
	ULONG DataLength;
	UCHAR Data[1];
} MGSL_RAWDATA, *PMGSL_RAWDATA;

/* data link layer statistics */
/* used with MgslDlGetStats() */

typedef struct _MGSL_DLSTATS
{
	ULONG Link;				/* link instance identifier */

   ULONG	State;			/* link layer connection state */
   ULONG	Status;			/* bit mapped flags for link status */

   ULONG	RxREJ;			/* received reject frames */
   ULONG	RxFRMR;			/* received frame rejects */
   ULONG	RxInvalidControl;	/* rx FRMR with invalid/unsupported ctrl field */
   ULONG	RxInvalidInfo;		/* rx FRMR with info field not allowed */
   ULONG	RxInvalidISize;	/* rx FRMR with info field too big  */
   ULONG	RxInvalidNr;		/* rx FRMR with invalid NR sequence number */
   ULONG	RxRSET;			/* rx RSET frames */
   ULONG	RxTEST;			/* rx TEST frames */

   ULONG	TxREJ;			/* transmitted reject frames */
   ULONG	TxFRMR;			/* transmitted frame rejects */
   ULONG	TxInvalidControl;	/* tx FRMR with invalid/unsupported ctrl field */
   ULONG	TxInvalidInfo;		/* tx FRMR with info field not allowed */
   ULONG	TxInvalidISize;	/* tx FRMR with info field too big  */
   ULONG	TxInvalidNr;		/* tx FRMR with invalid NR sequence number */
   ULONG	TxRSET;			/* tx RSET frames */
   ULONG	TxTEST;			/* tx TEST frames */

   ULONG	ResponseTimeouts;
   ULONG	IdleTimeouts;

   ULONG	TxPending;		/* I-frames pending transmission */
   ULONG	TxPendingAck;	/* I-frames pending acknowledgement */
   ULONG	TxAcked;			/* Acknowledged I-frames */

	ULONG	RxInfo;			/* received info frames */
	ULONG	RxInfoBytes;	/* received info field bytes */
	ULONG	RxUI;				/* received UI frames */
	ULONG	RxUIBytes;		/* received UI info field bytes */

	ULONG	Reserved1;		/* must be zero */
	ULONG	Reserved2;		/* must be zero */

} MGSL_DLSTATS, *PMGSL_DLSTATS;

/* data link address lists */

#define ADDRESS_LOCAL     0
#define ADDRESS_REMOTE    1

typedef struct _MGSL_ADDRESS_LIST
{
	ULONG Link;    /* data link identifier */
	ULONG ListID;  /* ADDRESS_REMOTE or ADDRESS_LOCAL */
	ULONG Length;  /* length in bytes of each address */
	ULONG Count;   /* count of addresses in data */
	UCHAR Data[1]; /* variable length set of addresses */

} MGSL_ADDRESS_LIST, *PMGSL_ADDRESS_LIST;

/* data link layer states */

#define DLSTATE_RELEASED	 			0
#define DLSTATE_WAIT_INIT				1
#define DLSTATE_INIT_MODE				2
#define DLSTATE_WAIT_ESTABLISH		3
#define DLSTATE_WAIT_RELEASE			4
#define DLSTATE_ESTABLISHED			5
#define DLSTATE_TIMER_RECOVERY		6
#define DLSTATE_COUNT					7


/* data link layer events */

#define DLEVENT_ESTABLISH_REQ			0
#define DLEVENT_RELEASE_REQ			1
#define DLEVENT_INIT_MODE_REQ			2
#define DLEVENT_DATA_REQ				3
#define DLEVENT_UNITDATA_REQ			4
#define DLEVENT_IFRAME_QUEUED			5
#define DLEVENT_UIFRAME_QUEUED		6
#define DLEVENT_SNRM						7
#define DLEVENT_SNRME					8
#define DLEVENT_SARM						9
#define DLEVENT_SARME					10
#define DLEVENT_SABM						11
#define DLEVENT_SABME					12
#define DLEVENT_DISC						13
#define DLEVENT_UA						14
#define DLEVENT_DM						15
#define DLEVENT_UI						16
#define DLEVENT_FRMR						17
#define DLEVENT_RR						18
#define DLEVENT_REJ						19
#define DLEVENT_RNR						20
#define DLEVENT_IFRAME					21
#define DLEVENT_SIM						22
#define DLEVENT_RIM						23
#define DLEVENT_RSET						24
#define DLEVENT_SREJ						25
#define DLEVENT_RD						26
#define DLEVENT_UP						27
#define DLEVENT_XID						28
#define DLEVENT_TEST						29
#define DLEVENT_CFGR						30
#define DLEVENT_BCN						31
#define DLEVENT_RESPONSE_TIMEOUT		32
#define DLEVENT_IDLE_TIMEOUT			33
#define DLEVENT_OWN_RCV_BUSY			34
#define DLEVENT_OWN_RCV_READY			35
#define DLEVENT_RETRY_LIMIT			36
#define DLEVENT_INVALID_CONTROL		37
#define DLEVENT_INVALID_INFO			38
#define DLEVENT_INVALID_ISIZE			39
#define DLEVENT_INVALID_NR				40
#define DLEVENT_COUNT					41

/* data link layer flag/status bits */

#define DLFLAG_SNRM					0x00000001
#define DLFLAG_SNRME					0x00000002
#define DLFLAG_NRM					(DLFLAG_SNRM+DLFLAG_SNRME)
#define DLFLAG_SARM					0x00000004
#define DLFLAG_SARME					0x00000008
#define DLFLAG_ARM					(DLFLAG_SARM+DLFLAG_SARME)
#define DLFLAG_SABM					0x00000010
#define DLFLAG_SABME					0x00000020
#define DLFLAG_ABM					(DLFLAG_SABM+DLFLAG_SABME)
#define DLFLAG_SREJ					0x00000040
#define DLFLAG_RSET					0x00000080
#define DLFLAG_XID					0x00000100
#define DLFLAG_TEST					0x00000200
#define DLFLAG_UP						0x00000400
#define DLFLAG_SIM					0x00000800
#define DLFLAG_RD						0x00001000
#define DLFLAG_REJ					0x00002000
#define DLFLAG_BCN					0x00004000
#define DLFLAG_CFGR					0x00008000
#define DLFLAG_PRIMARY				0x00010000
#define DLFLAG_DEFAULT				0x00020000
#define DLFLAG_DUPLEX				0x00040000
#define DLFLAG_MULTI_SREJ			0x00080000
#define DLFLAG_ENABLE				0x00100000
#define DLFLAG_ACCEPT				0x00200000
#define DLFLAG_BROADCAST			0x00400000
#define DLFLAG_POLL					0x00800000
#define DLFLAG_CANCEL				0x01000000
#define DLFLAG_ERROR					DLFLAG_CANCEL
#define DLFLAG_ALL_ADDRESS			0x02000000
#define DLFLAG_DEFAULT_COMMAND      DLFLAG_ALL_ADDRESS
#define DLFLAG_DEFAULT_RESPONSE		0x04000000
#define DLFLAG_INLINE_ADDRESS		0x08000000
#define DLFLAG_MANUAL_XID           0x10000000
#define DLFLAG_NO_PF                0x20000000
#define DLFLAG_AUTO_ADDRESS         0x40000000

#define DLPHYSFLAG_LOOP_CONTROLLER	0x00000001
#define DLPHYSFLAG_EXTENDED_ADDRESS	0x00000002

#define DLSTATUS_SNRM					0x00000001
#define DLSTATUS_SNRME					0x00000002
#define DLSTATUS_NRM						(DLSTATUS_SNRM+DLSTATUS_SNRME)
#define DLSTATUS_SARM					0x00000004
#define DLSTATUS_SARME					0x00000008
#define DLSTATUS_ARM						(DLSTATUS_SARM+DLSTATUS_SARME)
#define DLSTATUS_SABM					0x00000010
#define DLSTATUS_SABME					0x00000020
#define DLSTATUS_ABM						(DLSTATUS_SABM+DLSTATUS_SABME)
#define DLSTATUS_PEER_RCV_BUSY		0x00000040
#define DLSTATUS_REJ_RECOVERY			0x00000080
#define DLSTATUS_OWN_RCV_BUSY			0x00000100
#define DLSTATUS_REESTABLISH			0x00000200
#define DLSTATUS_PENDING_RELEASE		0x00000400
#define DLSTATUS_ACK_PENDING			0x00000800
#define DLSTATUS_TX_POLL				0x00002000
#define DLSTATUS_RX_POLL				0x00004000
#define DLSTATUS_FRMR					0x00008000
#define DLSTATUS_REQUEST_DISC			0x00010000
#define DLSTATUS_ENABLE_CHECKPOINT	0x00020000
#define DLSTATUS_INHIBIT_CP			0x00040000
#define DLSTATUS_SREJ_RECOVERY		0x00080000
#define DLSTATUS_INHIBIT_SREJ			0x00100000
#define DLSTATUS_RIM						0x00200000
#define DLSTATUS_SIM						0x00400000
#define DLSTATUS_BEACON_TEST			0x00800000
#define DLSTATUS_MONITOR_MODE			0x01000000
#define DLSTATUS_WRAP					0x02000000
#define DLSTATUS_MODIFIED_LINK_TEST	0x04000000
#define DLSTATUS_ENQUIRY_PENDING		0x08000000

#define MODESET_MASK	(DLFLAG_SABME + DLFLAG_SABM + DLFLAG_SNRME \
			 + DLFLAG_SNRM + DLFLAG_SARME + DLFLAG_SARM)
#define RXCOND_MASK (DLSTATUS_PEER_RCV_BUSY + DLSTATUS_OWN_RCV_BUSY + DLSTATUS_REJ_RECOVERY)
#define EXCEPTION_MASK (RXCOND_MASK + DLSTATUS_ACK_PENDING + \
			DLSTATUS_FRMR + DLSTATUS_REQUEST_DISC + DLSTATUS_ENABLE_CHECKPOINT + \
			DLSTATUS_SREJ_RECOVERY + DLSTATUS_INHIBIT_CP)

/* data link primitive types */
/* used with Type member of MGSL_PRIMITIVE structure */

#define DLERROR_CANCEL					0
#define DLERROR_INSUFFICIENT_BUFFER	1
#define DLERROR_OVERFLOW				2
#define DLCONFIRM_CONFIGURE			3
#define DLCONFIRM_ESTABLISH			4
#define DLCONFIRM_INIT_MODE			5
#define DLCONFIRM_RELEASE				6
#define DLCONFIRM_TEST					7
#define DLCONFIRM_XID					8
#define DLINDICATION_BEACON			9
#define DLINDICATION_DATA				10
#define DLINDICATION_DATA_DONE      11
#define DLINDICATION_ESTABLISH		12
#define DLINDICATION_INIT_MODE		13
#define DLINDICATION_RAWDATA			14
#define DLINDICATION_RCV_BUSY			15
#define DLINDICATION_RELEASE			16
#define DLINDICATION_SEND_READY		17
#define DLINDICATION_UNITDATA			18
#define DLINDICATION_UNITDATA_DONE  19
#define DLINDICATION_XID				20
#define DLRESPONSE_XID					21
#define DLREQUEST_CONFIGURE			22
#define DLREQUEST_DATA					23
#define DLREQUEST_ESTABLISH			24
#define DLREQUEST_INIT_MODE			25
#define DLREQUEST_RAWDATA				26
#define DLREQUEST_RCV_BUSY				27
#define DLREQUEST_RELEASE				28
#define DLREQUEST_TEST					29
#define DLREQUEST_UNITDATA				30
#define DLREQUEST_XID					31

#define CFGR_ON				0x01
#define CFGR_OFF				0x00
#define CFGR_CLEAR			0x00
#define CFGR_BEACON_TEST	0x02
#define CFGR_MONITOR_MODE	0x04
#define CFGR_WRAP				0x08
#define CFGR_SELF_TEST		0x0a
#define CFGR_LINK_TEST		0x0c

/* macros for building MGSL_OPT_TDM value */

/* TDM sync frame [bit 20] */
#define TDM_SYNC_FRAME_OFF      0
#define TDM_SYNC_FRAME_ON       (1 << 20)

/* TDM sync to data delay [bits 19:18] */
#define TDM_SYNC_DELAY_NONE        0
#define TDM_SYNC_DELAY_1BIT        (1 << 18)
#define TDM_SYNC_DELAY_2BITS       (2 << 18)

/* TDM transmit sync width [bit 17] */
#define TDM_TX_SYNC_WIDTH_SLOT     0
#define TDM_TX_SYNC_WIDTH_BIT      (1 << 17)

/* TDM Sync polarity [bit 16] */
#define TDM_SYNC_POLARITY_NORMAL   0
#define TDM_SYNC_POLARITY_INVERT   (1 << 16)

/* TDM RX FRAME COUNT [bits 15:8] */
#define TDM_RX_FRAME_COUNT(a) (((a)-1) << 8)

/* TDM slot count [bits 7:3] : 0=384 slots, 1-31 = 2-32 slots */
#define TDM_SLOT_COUNT(a) (((a) == 384) ? 0 : ((((a)-1) & 0x1f) << 3))

/* TDM slot size [bits 2:0] */
#define TDM_SLOT_SIZE_8BITS   1
#define TDM_SLOT_SIZE_12BITS  2
#define TDM_SLOT_SIZE_16BITS  3
#define TDM_SLOT_SIZE_20BITS  4
#define TDM_SLOT_SIZE_24BITS  5
#define TDM_SLOT_SIZE_28BITS  6
#define TDM_SLOT_SIZE_32BITS  7

typedef struct _DATALINK_TRACE_EVENT
{
	ULONG Link;
	ULONG	Va;
	ULONG Vs;
	ULONG Vr;
	ULONG Event;
	ULONG State;
} DATALINK_TRACE_EVENT, *PDATALINK_TRACE_EVENT;

typedef struct _DATALINK_TRACE_FRAME
{
	ULONG Link;
	UCHAR Transmit;
	UCHAR Command;
	UCHAR ControlField[2];
	ULONG ControlLength;

} DATALINK_TRACE_FRAME, *PDATALINK_TRACE_FRAME;


/*
** The following structures define the format of API trace data
*/

typedef struct API_TRACE_CANCEL_RECEIVE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_CANCEL_RECEIVE_CALL, *PAPI_TRACE_CANCEL_RECEIVE_CALL;

typedef struct API_TRACE_CANCEL_RECEIVE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_CANCEL_RECEIVE_RETURN, *PAPI_TRACE_CANCEL_RECEIVE_RETURN;

typedef struct API_TRACE_CANCEL_TRANSMIT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_CANCEL_TRANSMIT_CALL, *PAPI_TRACE_CANCEL_TRANSMIT_CALL;

typedef struct API_TRACE_CANCEL_TRANSMIT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_CANCEL_TRANSMIT_RETURN, *PAPI_TRACE_CANCEL_TRANSMIT_RETURN;

typedef struct API_TRACE_CANCEL_WAIT_EVENT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_CANCEL_WAIT_EVENT_CALL, *PAPI_TRACE_CANCEL_WAIT_EVENT_CALL;

typedef struct API_TRACE_CANCEL_WAIT_EVENT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_CANCEL_WAIT_EVENT_RETURN, *PAPI_TRACE_CANCEL_WAIT_EVENT_RETURN;

typedef struct API_TRACE_ENABLE_RECEIVER_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	BOOL EnableFlag;

} API_TRACE_ENABLE_RECEIVER_CALL, *PAPI_TRACE_ENABLE_RECEIVER_CALL;

typedef struct API_TRACE_ENABLE_RECEIVER_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_ENABLE_RECEIVER_RETURN, *PAPI_TRACE_ENABLE_RECEIVER_RETURN;

typedef struct API_TRACE_ENABLE_TRANSMITTER_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	BOOL EnableFlag;

} API_TRACE_ENABLE_TRANSMITTER_CALL, *PAPI_TRACE_ENABLE_TRANSMITTER_CALL;

typedef struct API_TRACE_ENABLE_TRANSMITTER_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_ENABLE_TRANSMITTER_RETURN, *PAPI_TRACE_ENABLE_TRANSMITTER_RETURN;

typedef struct API_TRACE_GET_OPTION_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	UINT  Option;
	UINT  *Value;

} API_TRACE_GET_OPTION_CALL, *PAPI_TRACE_GET_OPTION_CALL;

typedef struct API_TRACE_GET_OPTION_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	UINT  Option;
	UINT  Value;

} API_TRACE_GET_OPTION_RETURN, *PAPI_TRACE_GET_OPTION_RETURN;

typedef struct API_TRACE_GET_PARAMS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_PARAMS pParams;

} API_TRACE_GET_PARAMS_CALL, *PAPI_TRACE_GET_PARAMS_CALL;

typedef struct API_TRACE_GET_PARAMS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	MGSL_PARAMS Params;

} API_TRACE_GET_PARAMS_RETURN, *PAPI_TRACE_GET_PARAMS_RETURN;

typedef struct API_TRACE_GET_SERIAL_SIGNALS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PUCHAR pSerialSignals;

} API_TRACE_GET_SERIAL_SIGNALS_CALL, *PAPI_TRACE_GET_SERIAL_SIGNALS_CALL;

typedef struct API_TRACE_GET_SERIAL_SIGNALS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	UCHAR SerialSignals;

} API_TRACE_GET_SERIAL_SIGNALS_RETURN, *PAPI_TRACE_GET_SERIAL_SIGNALS_RETURN;

typedef struct API_TRACE_GET_TRACE_LEVEL_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PULONG pTraceLevel;

} API_TRACE_GET_TRACE_LEVEL_CALL, *PAPI_TRACE_GET_TRACE_LEVEL_CALL;

typedef struct API_TRACE_GET_TRACE_LEVEL_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	ULONG TraceLevel;

} API_TRACE_GET_TRACE_LEVEL_RETURN, *PAPI_TRACE_GET_TRACE_LEVEL_RETURN;

typedef struct API_TRACE_RECEIVE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_RECEIVE_REQUEST pRcvReq;
	MGSL_RECEIVE_REQUEST RcvReq;
	LPOVERLAPPED pOverlapped;

} API_TRACE_RECEIVE_CALL, *PAPI_TRACE_RECEIVE_CALL;

typedef struct API_TRACE_RECEIVE_COMPLETE
{
	MGSL_RECEIVE_REQUEST RcvReq;

} API_TRACE_RECEIVE_COMPLETE, *PAPI_TRACE_RECEIVE_COMPLETE;

typedef struct API_TRACE_RECEIVE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	MGSL_RECEIVE_REQUEST RcvReq;

} API_TRACE_RECEIVE_RETURN, *PAPI_TRACE_RECEIVE_RETURN;

typedef struct API_TRACE_READ_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	unsigned char *buf;
	int size;

} API_TRACE_READ_CALL;

typedef struct API_TRACE_READ_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	int ReturnCode;

} API_TRACE_READ_RETURN;

typedef struct API_TRACE_READWS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	unsigned char *buf;
	int size;
	int *status;
} API_TRACE_READWS_CALL;

typedef struct API_TRACE_READWS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	int ReturnCode;
	int status;

} API_TRACE_READWS_RETURN;

typedef struct API_TRACE_RESET_TRACE_BUFFERS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_RESET_TRACE_BUFFERS_CALL, *PAPI_TRACE_RESET_TRACE_BUFFERS_CALL;

typedef struct API_TRACE_RESET_TRACE_BUFFERS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_RESET_TRACE_BUFFERS_RETURN, *PAPI_TRACE_RESET_TRACE_BUFFERS_RETURN;

typedef struct API_TRACE_SET_IDLE_MODE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG IdleMode;

} API_TRACE_SET_IDLE_MODE_CALL, *PAPI_TRACE_SET_IDLE_MODE_CALL;

typedef struct API_TRACE_SET_IDLE_MODE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_SET_IDLE_MODE_RETURN, *PAPI_TRACE_SET_IDLE_MODE_RETURN;

typedef struct API_TRACE_SET_OPTION_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	UINT  Option;
	UINT  Value;

} API_TRACE_SET_OPTION_CALL, *PAPI_TRACE_SET_OPTION_CALL;

typedef struct API_TRACE_SET_OPTION_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_SET_OPTION_RETURN, *PAPI_TRACE_SET_OPTION_RETURN;

typedef struct API_TRACE_SET_SERIAL_SIGNALS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	UCHAR SerialSignals;

} API_TRACE_SET_SERIAL_SIGNALS_CALL, *PAPI_TRACE_SET_SERIAL_SIGNALS_CALL;

typedef struct API_TRACE_SET_SERIAL_SIGNALS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_SET_SERIAL_SIGNALS_RETURN, *PAPI_TRACE_SET_SERIAL_SIGNALS_RETURN;

typedef struct API_TRACE_SET_PARAMS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_PARAMS pParams;
	MGSL_PARAMS Params;

} API_TRACE_SET_PARAMS_CALL, *PAPI_TRACE_SET_PARAMS_CALL;

typedef struct API_TRACE_SET_PARAMS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_SET_PARAMS_RETURN, *PAPI_TRACE_SET_PARAMS_RETURN;

typedef struct API_TRACE_SET_TRACE_LEVEL_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG TraceLevel;

} API_TRACE_SET_TRACE_LEVEL_CALL, *PAPI_TRACE_SET_TRACE_LEVEL_CALL;

typedef struct API_TRACE_SET_TRACE_LEVEL_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_SET_TRACE_LEVEL_RETURN, *PAPI_TRACE_SET_TRACE_LEVEL_RETURN;

typedef struct API_TRACE_TRANSMIT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PUCHAR pBuffer;
	ULONG BufferSize;
	PULONG pStatus;
	LPOVERLAPPED pOverlapped;

} API_TRACE_TRANSMIT_CALL, *PAPI_TRACE_TRANSMIT_CALL;

typedef struct API_TRACE_TRANSMIT_COMPLETE
{
	ULONG Status;

} API_TRACE_TRANSMIT_COMPLETE, *PAPI_TRACE_TRANSMIT_COMPLETE;

typedef struct API_TRACE_TRANSMIT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	ULONG Status;

} API_TRACE_TRANSMIT_RETURN, *PAPI_TRACE_TRANSMIT_RETURN;

typedef struct API_TRACE_WRITE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	unsigned char *buf;
	int size;

} API_TRACE_WRITE_CALL;

typedef struct API_TRACE_WRITE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	int ReturnCode;

} API_TRACE_WRITE_RETURN;

typedef struct API_TRACE_WAIT_ALL_SENT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_WAIT_ALL_SENT_CALL;

typedef struct API_TRACE_WAIT_ALL_SENT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	int ReturnCode;

} API_TRACE_WAIT_ALL_SENT_RETURN;

typedef struct API_TRACE_WAIT_EVENT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG EventMask;
	PULONG pEvents;
	LPOVERLAPPED pOverlapped;

} API_TRACE_WAIT_EVENT_CALL, *PAPI_TRACE_WAIT_EVENT_CALL;

typedef struct API_TRACE_WAIT_EVENT_COMPLETE
{
	ULONG Events;

} API_TRACE_WAIT_EVENT_COMPLETE, *PAPI_TRACE_WAIT_EVENT_COMPLETE;

typedef struct API_TRACE_WAIT_EVENT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	ULONG Events;

} API_TRACE_WAIT_EVENT_RETURN, *PAPI_TRACE_WAIT_EVENT_RETURN;

typedef struct API_TRACE_GET_ASSIGNED_RESOURCES_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_ASSIGNED_RESOURCES pResources;

} API_TRACE_GET_ASSIGNED_RESOURCES_CALL, *PAPI_TRACE_GET_ASSIGNED_RESOURCES_CALL;

typedef struct API_TRACE_GET_ASSIGNED_RESOURCES_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	MGSL_ASSIGNED_RESOURCES Resources;

} API_TRACE_GET_ASSIGNED_RESOURCES_RETURN, *PAPI_TRACE_GET_ASSIGNED_RESOURCES_RETURN;


typedef struct API_TRACE_LOOPMODE_SEND_DONE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
} API_TRACE_LOOPMODE_SEND_DONE_CALL, *PAPI_TRACE_LOOPMODE_SEND_DONE_CALL;

typedef struct API_TRACE_LOOPMODE_SEND_DONE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
} API_TRACE_LOOPMODE_SEND_DONE_RETURN, *PAPI_TRACE_LOOPMODE_SEND_DONE_RETURN;

typedef struct API_TRACE_SET_GPIO_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	GPIO_DESC *pGpio;
	GPIO_DESC Gpio;

} API_TRACE_SET_GPIO_CALL, *PAPI_TRACE_SET_GPIO_CALL;

typedef struct API_TRACE_SET_GPIO_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_SET_GPIO_RETURN, *PAPI_TRACE_SET_GPIO_RETURN;

typedef struct API_TRACE_GET_GPIO_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	GPIO_DESC *pGpio;

} API_TRACE_GET_GPIO_CALL, *PAPI_TRACE_GET_GPIO_CALL;

typedef struct API_TRACE_GET_GPIO_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	GPIO_DESC Gpio;

} API_TRACE_GET_GPIO_RETURN, *PAPI_TRACE_GET_GPIO_RETURN;

typedef struct API_TRACE_WAIT_GPIO_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG EventMask;
	GPIO_DESC *pGpio;
	GPIO_DESC Gpio;
	LPOVERLAPPED pOverlapped;

} API_TRACE_WAIT_GPIO_CALL, *PAPI_TRACE_WAIT_GPIO_CALL;

typedef struct API_TRACE_WAIT_GPIO_COMPLETE
{
	GPIO_DESC Gpio;

} API_TRACE_WAIT_GPIO_COMPLETE, *PAPI_TRACE_WAIT_GPIO_COMPLETE;

typedef struct API_TRACE_WAIT_GPIO_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	GPIO_DESC Gpio;

} API_TRACE_WAIT_GPIO_RETURN, *PAPI_TRACE_WAIT_GPIO_RETURN;

typedef struct API_TRACE_CANCEL_WAIT_GPIO_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_CANCEL_WAIT_GPIO_CALL, *PAPI_TRACE_CANCEL_WAIT_GPIO_CALL;

typedef struct API_TRACE_CANCEL_WAIT_GPIO_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_CANCEL_WAIT_GPIO_RETURN, *PAPI_TRACE_CANCEL_WAIT_GPIO_RETURN;

typedef struct API_TRACE_DL_ALLOCATE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG* pLink;

} API_TRACE_DL_ALLOCATE_CALL, *PAPI_TRACE_DL_ALLOCATE_CALL;

typedef struct API_TRACE_DL_ALLOCATE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	ULONG Link;

} API_TRACE_DL_ALLOCATE_RETURN, *PAPI_TRACE_DL_ALLOCATE_RETURN;

typedef struct API_TRACE_DL_CALL_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	MGSL_DLPRIMITIVE* pPrimitive;
	MGSL_DLPRIMITIVE Primitive;

} API_TRACE_DL_CALL_CALL, *PAPI_TRACE_DL_CALL_CALL;

typedef struct API_TRACE_DL_CALL_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	MGSL_DLPRIMITIVE* pPrimitive;
	ULONG Link;

} API_TRACE_DL_CALL_RETURN, *PAPI_TRACE_DL_CALL_RETURN;

typedef struct API_TRACE_DL_CANCEL_WAIT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;

} API_TRACE_DL_CANCEL_WAIT_CALL, *PAPI_TRACE_DL_CANCEL_WAIT_CALL;

typedef struct API_TRACE_DL_CANCEL_WAIT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;

} API_TRACE_DL_CANCEL_WAIT_RETURN, *PAPI_TRACE_DL_CANCEL_WAIT_RETURN;

typedef struct API_TRACE_DL_FREE_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG Link;

} API_TRACE_DL_FREE_CALL, *PAPI_TRACE_DL_FREE_CALL;

typedef struct API_TRACE_DL_FREE_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	ULONG Link;

} API_TRACE_DL_FREE_RETURN, *PAPI_TRACE_DL_FREE_RETURN;

typedef struct API_TRACE_DL_GET_PARAMS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG Link;
	PMGSL_DLPARAMS pParams;

} API_TRACE_DL_GET_PARAMS_CALL, *PAPI_TRACE_DL_GET_PARAMS_CALL;

typedef struct API_TRACE_DL_GET_PARAMS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	PMGSL_DLPARAMS pParams;
	MGSL_DLPARAMS Params;

} API_TRACE_DL_GET_PARAMS_RETURN, *PAPI_TRACE_DL_GET_PARAMS_RETURN;

typedef struct API_TRACE_DL_GET_PHYSPARAMS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_DLPHYSPARAMS pParams;

} API_TRACE_DL_GET_PHYSPARAMS_CALL, *PAPI_TRACE_DL_GET_PHYSPARAMS_CALL;

typedef struct API_TRACE_DL_GET_PHYSPARAMS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	PMGSL_DLPHYSPARAMS pParams;
	MGSL_DLPHYSPARAMS Params;

} API_TRACE_DL_GET_PHYSPARAMS_RETURN, *PAPI_TRACE_DL_GET_PHYSPARAMS_RETURN;

typedef struct API_TRACE_DL_GET_STATS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG Link;
	PMGSL_DLSTATS pStats;

} API_TRACE_DL_GET_STATS_CALL, *PAPI_TRACE_DL_GET_STATS_CALL;

typedef struct API_TRACE_DL_GET_STATS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	PMGSL_DLSTATS pStats;
	MGSL_DLSTATS Stats;

} API_TRACE_DL_GET_STATS_RETURN, *PAPI_TRACE_DL_GET_STATS_RETURN;

typedef struct API_TRACE_DL_RESET_STATS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG Link;

} API_TRACE_DL_RESET_STATS_CALL, *PAPI_TRACE_DL_RESET_STATS_CALL;

typedef struct API_TRACE_DL_RESET_STATS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	ULONG Link;

} API_TRACE_DL_RESET_STATS_RETURN, *PAPI_TRACE_DL_RESET_STATS_RETURN;

typedef struct API_TRACE_DL_SET_ADDRLIST_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_ADDRESS_LIST pList;
	MGSL_ADDRESS_LIST List;

} API_TRACE_DL_SET_ADDRLIST_CALL, *PAPI_TRACE_DL_SET_ADDRLIST_CALL;

typedef struct API_TRACE_DL_SET_ADDRLIST_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	PMGSL_ADDRESS_LIST pList;
	ULONG Link;

} API_TRACE_DL_SET_ADDRLIST_RETURN, *PAPI_TRACE_DL_SET_ADDRLIST_RETURN;

typedef struct API_TRACE_DL_SET_PARAMS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_DLPARAMS pParams;
	MGSL_DLPARAMS Params;

} API_TRACE_DL_SET_PARAMS_CALL, *PAPI_TRACE_DL_SET_PARAMS_CALL;

typedef struct API_TRACE_DL_SET_PARAMS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	PMGSL_DLPARAMS pParams;
	ULONG Link;

} API_TRACE_DL_SET_PARAMS_RETURN, *PAPI_TRACE_DL_SET_PARAMS_RETURN;

typedef struct API_TRACE_DL_SET_PHYSPARAMS_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	PMGSL_DLPHYSPARAMS pParams;
	MGSL_DLPHYSPARAMS Params;

} API_TRACE_DL_SET_PHYSPARAMS_CALL, *PAPI_TRACE_DL_SET_PHYSPARAMS_CALL;

typedef struct API_TRACE_DL_SET_PHYSPARAMS_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	PMGSL_DLPHYSPARAMS pParams;

} API_TRACE_DL_SET_PHYSPARAMS_RETURN, *PAPI_TRACE_DL_SET_PHYSPARAMS_RETURN;

typedef struct API_TRACE_DL_WAIT_CALL
{
	ULONG ProcessId;
	ULONG ThreadId;
	MGSL_DLPRIMITIVE* pPrimitive;
	ULONG Size;
	LPOVERLAPPED pOverlapped;

} API_TRACE_DL_WAIT_CALL, *PAPI_TRACE_DL_WAIT_CALL;

typedef struct API_TRACE_DL_WAIT_COMPLETE
{
	MGSL_DLPRIMITIVE Primitive;

} API_TRACE_DL_WAIT_COMPLETE, *PAPI_TRACE_DL_WAIT_COMPLETE;

typedef struct API_TRACE_DL_WAIT_RETURN
{
	ULONG ProcessId;
	ULONG ThreadId;
	ULONG ReturnCode;
	MGSL_DLPRIMITIVE* pPrimitive;
	MGSL_DLPRIMITIVE Primitive;

} API_TRACE_DL_WAIT_RETURN, *PAPI_TRACE_DL_WAIT_RETURN;


/* Driver Interface API Trace Structures (Start) */

typedef union 
{
	void *ptr;
	ULONG ulong;
	char data[1];
} API_TRACE_DI_VALUE;

/* all driver interface API trace events use API_TRACE_DI as the
 * trace event data. Args contains a variable number of pointer or
 * ULONG values, dependant on the specific API call traced.
 * Optional trailing data of arbitrary length follows the pointer
 * and ULONG args at Args[ArgCount].data.
 * See the trace program source for details on formatting the
 * API_TRACE_DI contents for the driver interface API calls.
 */
typedef struct _API_TRACE_DI
{
	void *Handle;
	void *Context;
	API_TRACE_DI_VALUE Args[1];
} API_TRACE_DI;

/* Driver Interface API Trace Structures (End) */


typedef struct _MGSL_PORT
{
	ULONG PortID;
	ULONG DeviceID;
	ULONG BusType;
	char DeviceName[25];

} MGSL_PORT, *PMGSL_PORT;

typedef struct _MGSL_PCI_ADAPTER
{
	ULONG BusNumber;
	ULONG DeviceNumber;

} MGSL_PCI_ADAPTER, *PMGSL_PCI_ADAPTER;

typedef struct _MGSL_PCI_ADAPTER_EX
{
	ULONG BusNumber;
	ULONG DeviceNumber;
	ULONG DeviceID;

} MGSL_PCI_ADAPTER_EX, *PMGSL_PCI_ADAPTER_EX;

#define SerialSignal_DCD            0x01     /* Data Carrier Detect */
#define SerialSignal_TXD            0x02     /* Transmit Data */
#define SerialSignal_RI             0x04     /* Ring Indicator */
#define SerialSignal_RXD            0x08     /* Receive Data */
#define SerialSignal_CTS            0x10     /* Clear to Send */
#define SerialSignal_RTS            0x20     /* Request to Send */
#define SerialSignal_DSR            0x40     /* Data Set Ready */
#define SerialSignal_DTR            0x80     /* Data Terminal Ready */


#if defined(__cplusplus)
extern "C"
{
#endif

/*
** Device Access Functions
*/

ULONG __stdcall MgslOpen( ULONG PortID, PHANDLE pHandle );
ULONG __stdcall MgslOpenDiagnostics( ULONG PortID, PHANDLE pHandle, PULONG pStatus );
ULONG __stdcall MgslOpenDiagnosticsEx( ULONG PortID, PHANDLE pHandle, PULONG pStatus,
	PMGSL_ASSIGNED_RESOURCES pResources );
ULONG __stdcall MgslOpenTraceHandle( ULONG PortID, PHANDLE pHandle );
ULONG __stdcall MgslOpenByName( char *PortName, PHANDLE pHandle );
ULONG __stdcall MgslOpenDiagnosticsByName( char *PortName, PHANDLE pHandle, PULONG pStatus,
	PMGSL_ASSIGNED_RESOURCES pResources );
ULONG __stdcall MgslOpenTraceHandleByName( char *PortName, PHANDLE pHandle );
ULONG __stdcall MgslClose( HANDLE hDevice );


/*
** Device Configuration Functions
*/

ULONG __stdcall MgslSetParams( HANDLE hDevice, PMGSL_PARAMS pParams );
ULONG __stdcall MgslGetParams( HANDLE hDevice, PMGSL_PARAMS pParams );
ULONG __stdcall MgslSetOption( HANDLE hDevice, UINT option_id, UINT value );
ULONG __stdcall MgslGetOption( HANDLE hDevice, UINT option_id, UINT *value );
ULONG __stdcall MgslSetPortLoadOption( ULONG PortID, char *name, UINT value );
ULONG __stdcall MgslGetPortLoadOption( ULONG PortID, char *name, UINT *value );

// Original port configuration functions are now deprecated,
// used the Ex versions.
ULONG __stdcall MgslGetPortConfig( ULONG PortID, PMGSL_PORT_CONFIG pConfig );
ULONG __stdcall MgslSetPortConfig( ULONG PortID, PMGSL_PORT_CONFIG pConfig );

ULONG __stdcall MgslGetPortConfigEx( ULONG PortID, PMGSL_PORT_CONFIG_EX pConfig );
ULONG __stdcall MgslSetPortConfigEx( ULONG PortID, PMGSL_PORT_CONFIG_EX pConfig );

/*
** Device Control/Status Functions
*/

ULONG __stdcall MgslSetSerialSignals( HANDLE hDevice, UCHAR NewSignals );
ULONG __stdcall MgslGetSerialSignals( HANDLE hDevice, PUCHAR pReturnedSignals );
ULONG __stdcall MgslWaitEvent( HANDLE hDevice, ULONG EventMask,
			PULONG pEvents, LPOVERLAPPED pOverlapped );
ULONG __stdcall MgslCancelWaitEvent( HANDLE hDevice );

/*
** General Purpose I/O Control/Status Functions
*/

ULONG __stdcall MgslSetGpio( HANDLE hDevice, GPIO_DESC *gpio );
ULONG __stdcall MgslGetGpio( HANDLE hDevice, GPIO_DESC *gpio );
ULONG __stdcall MgslWaitGpio( HANDLE hDevice, GPIO_DESC *gpio, LPOVERLAPPED ol );
ULONG __stdcall MgslCancelWaitGpio( HANDLE hDevice );

/*
** Data Communication Functions
*/

ULONG __stdcall MgslTransmit( HANDLE hDevice, PUCHAR pBuffer, ULONG BufferSize,
			PULONG pStatus, LPOVERLAPPED pOverlapped );
ULONG __stdcall MgslCancelTransmit( HANDLE hDevice );
ULONG __stdcall MgslEnableTransmitter( HANDLE hDevice, BOOL EnableFlag );
ULONG __stdcall MgslSetIdleMode( HANDLE hDevice, ULONG IdleMode );
int   __stdcall MgslWrite(HANDLE hDevice, unsigned char *buf, int size);
int   __stdcall MgslWaitAllSent(HANDLE hDevice);

ULONG __stdcall MgslReceive( HANDLE hDevice, PMGSL_RECEIVE_REQUEST pRcvReq, 
			LPOVERLAPPED pOverlapped );
ULONG __stdcall MgslCancelReceive( HANDLE hDevice );
ULONG __stdcall MgslEnableReceiver( HANDLE hDevice, BOOL EnableFlag );
int   __stdcall MgslRead(HANDLE hDevice, unsigned char *buf, int size);
int   __stdcall MgslReadWithStatus(HANDLE hDevice, unsigned char *buf, int size, int *status);

/*
** Trace Functions
*/

ULONG __stdcall MgslResetTraceBuffers( HANDLE hDevice );
ULONG __stdcall MgslSetTraceLevel( HANDLE hDevice, ULONG NewTraceLevel );
ULONG __stdcall MgslGetTraceLevel( HANDLE hDevice, PULONG pTraceLevel );
ULONG __stdcall MgslGetTraceEvent( HANDLE hDevice, PMGSL_TRACE_EVENT pEvent, 
			LPOVERLAPPED pOverlapped );
ULONG __stdcall MgslCancelGetTraceEvent( HANDLE hDevice );
ULONG __stdcall MgslPutTraceEvent( HANDLE hDevice, PMGSL_TRACE_EVENT pEvent );

/*
** Misc Functions
*/

ULONG __stdcall MgslInstallService( void );
ULONG __stdcall MgslRemoveService( void );
ULONG __stdcall MgslGetAssignedResources( HANDLE hDevice, PMGSL_ASSIGNED_RESOURCES pRes );
ULONG __stdcall MgslEnumeratePciAdapters( PMGSL_PCI_ADAPTER pAdapters, 
			ULONG BufferSize, PULONG AdapterCount );
ULONG __stdcall MgslEnumeratePciAdaptersEx( PMGSL_PCI_ADAPTER_EX pAdapters, 
			ULONG BufferSize, PULONG AdapterCount );
ULONG __stdcall MgslLoopModeSendDone( HANDLE hDevice );
ULONG __stdcall MgslEnumeratePorts(PMGSL_PORT pPorts, 
			ULONG BufferSize, PULONG pPortCount );

/* link layer API function prototypes */

ULONG __stdcall MgslDlAllocate(HANDLE hDevice, ULONG* Link);
ULONG __stdcall MgslDlCall(HANDLE hDevice, MGSL_DLPRIMITIVE* Primitive);
ULONG __stdcall MgslDlCancelWait(HANDLE hDevice);
ULONG __stdcall MgslDlFree(HANDLE hDevice, ULONG Link);
ULONG __stdcall MgslDlGetParams(HANDLE hDevice, PMGSL_DLPARAMS Params);
ULONG __stdcall MgslDlGetPhysParams(HANDLE hDevice, PMGSL_DLPHYSPARAMS Params);
ULONG __stdcall MgslDlGetStats(HANDLE hDevice, PMGSL_DLSTATS Stats);
ULONG __stdcall MgslDlResetStats(HANDLE hDevice, ULONG Link);
ULONG __stdcall MgslDlSetAddressList(HANDLE hDevice, PMGSL_ADDRESS_LIST List);
ULONG __stdcall MgslDlSetParams(HANDLE hDevice, PMGSL_DLPARAMS Params);
ULONG __stdcall MgslDlSetPhysParams(HANDLE hDevice, PMGSL_DLPHYSPARAMS Params);
ULONG __stdcall MgslDlWait(HANDLE hDevice, MGSL_DLPRIMITIVE* Primitive, LPOVERLAPPED pOverlapped);

#if defined(__cplusplus)
}
#endif

#pragma pack()

#endif
