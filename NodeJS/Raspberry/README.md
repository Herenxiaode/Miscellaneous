# Example PN532-I2C
```js
const PN532=require('./pn532-i2c.js');

PN532.openAsync(1).then(T=>T.SAMConfiguration(0x01)
	.then(o=>T.InListPassiveTarget(2))
	.then(o=>{console.log(o);return T.InDataExchange_AuthenticationA(o[0].NFCID,0xFFFFFFFFFFFF)})
	.then(o=>{console.log(o);return T.InDataExchange_read()})
	.then(o=>{console.log(o);return T.InDataExchange_write([0x11,0x22,0x33,0x44,0x55,0x66,0x77,0x88,0x11,0x22,0x33,0x44,0x55,0x66,0x77,0x88])})
	.then(console.log)
	.catch(console.log)
	.finally(_=>T.close())
).catch(console.log)
```