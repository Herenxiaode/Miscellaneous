/*
Miscellaneous
Diagnose			X	X 0x00 69
GetFirmwareVersion	X	X 0x02 73
GetGeneralStatus	X	X 0x04 74
ReadRegister		X	X 0x06 76
WriteRegister		X	X 0x08 78
ReadGPIO			X	X 0x0C 79
WriteGPIO			X	X 0x0E 81
SetSerialBaudRate	X	X 0x10 83
SetParameters		X	X 0x12 85
SAMConfiguration	X	X 0x14 89
PowerDown			X	X 0x16 98
RF communication
RFConfiguration		X	X 0x32 101
RFRegulationTest	X	X 0x58 107
Initiator
InJumpForDEP			X 0x56 108
InJumpForPSL			X 0x46 113
InListPassiveTarget		X 0x4A 115
InATR					X 0x50 122
InPSL					X 0x4E 125
InDataExchange			X 0x40 127
InCommunicateThru		X 0x42 136
InDeselect				X 0x44 139
InRelease				X 0x52 140
InSelect				X 0x54 141
InAutoPoll				X 0x60 144
Target
TgInitAsTarget			X 0x8C 151
TgSetGeneralBytes		X 0x92 158
TgGetData				X 0x86 160
TgSetData				X 0x8E 164
TgSetMetaData			X 0x94 166
TgGetInitiatorCommand	X 0x88 168
TgResponseToInitiator	X 0x90 170
TgGetTargetStatus		X 0x8A 172

const Diagnose=[0x00];//Get?:7F 81
const GetFirmwareVersion=[0x02];//Get X IC Ver Rev Support:D5 03 32 01 06 07
const SAMConfiguration=[0x14,0x01];//Get X:D5 03 32 01 06 07
const InListPassiveTarget=[0x4A,0x02,0x00];//寻卡,数量,参数C[,参数D]
*/
'use strict';
const fs=require('fs');
const ioctl=require('ioctl');

const I2C_SLAVE			=0x0703;
const I2C_SLAVE_FORCE	=0x0706;
const PN532_ADDR		=0x24;

const sleep=s=>new Promise(r=>setTimeout(r,s));

class PN532_I2C{
	constructor(ID){
		this.target=fs.openSync('/dev/i2c-'+ID,'r+');
		ioctl(this.target,I2C_SLAVE_FORCE,PN532_ADDR);
	}
	// sendBuffer(buffer){fs.writeSync(this.target,buffer,0,buffer.length,0);}
	processData(buffer){
// console.log(buffer);
		let len=buffer.length;//>=0xFF?0xFF:buffer.length;
		let o=Buffer.alloc(len+8);
		let sum=0xD4;
		for(let s=0;s<len;s++)sum+=buffer[s];
		o.writeUInt8(0x00,0);
		o.writeUInt16BE(0x00FF,1);
		o.writeUInt8(len+1,3);
		o.writeUInt8(0xFF-len,4);
		o.writeUInt8(0xD4,5);
		Buffer.from(buffer).copy(o,6);
		o.writeUInt8(0x100-(sum&0xFF),len+6);
// console.log(o);
		return o;
	}
	readData(){
		let buffer=Buffer.alloc(0xFF);//0x106
		fs.readSync(this.target,buffer,0,buffer.length,0);
		if(buffer[0]!=1)return false;
// console.log(buffer);
		let len=buffer[4];
		if(len==0)return false;
		if(len+buffer[5]!=0x100)throw new Error('I2CData Length Error');
		let sum=0;
		for(let s=6,l=len+s;s<l;s++)sum+=buffer[s];
		if((0x100-sum&0xFF)!=buffer[len+6])throw new Error('I2CData SUMC Error');
		return buffer.subarray(6,len+6);
	}
	async sendData(buffer){
		if(this.locked)throw new Error('PN532_I2C now is using,or try "await"');
		this.locked=true;
		let o=this.processData(buffer);
		fs.writeSync(this.target,o,0,o.length,0);
		do{await sleep(0);o=this.readData()}while(!o)
		this.locked=false;
		return o;
	}
	GetFirmwareVersion(){return this.sendData([0x02])}
	SAMConfiguration(type){return this.sendData([0x14,type||1])}
	async InListPassiveTarget(count,type){
		let o=await this.sendData([0x4A,count||1,type||0]);
		let os=[];count=o[2];
		for(let s=0,i=3;s<count;s++){
			let l=o[i+4];
			os.push({ID:o[i],NFCID:o.subarray(i+5,i+5+l)});
			i+=5+l;
		}
		return os;
	}
	InDataExchange_AuthenticationA(id,pass){
		let buffer=Buffer.alloc(14);
		buffer.writeUInt32BE(0x40016001);
		buffer.writeUIntBE(pass,4,6);
		id.copy(buffer,10);
		return this.sendData(buffer);
	}
	InDataExchange_read(){return this.sendData([0x40,0x01,0x30,0x01]);}
	InDataExchange_write(data){
		let buffer=Buffer.alloc(20);
		buffer.writeUInt32BE(0x4001A001);
		Buffer.from(data).copy(buffer,4);
		return this.sendData(buffer);
	}
	close(){fs.closeSync(this.target)}
}
module.exports={
	open:(id)=>{
		let o=new PN532_I2C(id);
		o.SAMConfiguration();
		return o;
	},
	openAsync:async(id)=>{
		let o=new PN532_I2C(id);
		await o.SAMConfiguration();
		return o;
	}
};