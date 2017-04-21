"use strict";

const path      = require('path');
const cp        = require('child_process');
const duplex    = require('duplexer');
const Server    = require('ssh-agent-js')
const KeyChain  = require('ssh-keychain')


const binpath   = path.join(__dirname, 'pageantbridge.exe');

class PageantTransport {

  constructor() {
    this.keychain = new KeyChain();
    this.server = new Server(this.keychain);
  }

  start () {
    var lnk = cp.spawn(binpath);
    var client = duplex(lnk.stdin, lnk.stdout);

    console.log("Spawning %s", binpath);
    this.server._new_client(client);

    var stop;
    process.on('cnyksEnd', () => {
      stop = true;
      lnk.kill();
      console.log("Waiting for server to die");
    });

  //if for whatever reason the bridge is down, start it again
    lnk.once("close", (code) => {
      console.log("Pagentbridge exited with code %s", code);
      if(code == 2 || stop) //already running, nothing smart to do
        return;
      this.start();
    });

    lnk.stderr.on("data", function(buffer){
      console.log("From process got", buffer.toString('ascii'));
    });

  }
}


module.exports = PageantTransport;
