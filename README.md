PageantBridge is a proxy wrapper (as standard strfor pageant/putty messaging protocol (using memorymap & winapi SendMessage/Postmessage)

# Motivation
I wanted to create an alternative to ssh-agent / pageant (see nwagent).

# How to use
Spawn pageantbridge.exe (make sure that its stdin is redirected)
This creates a window (not shown) using Windows API registered as Pageant class.

Incoming request are forwarded to pageantbridge.exe stdout.
Once processed, provide a response to pageantbridge.exe stdin.

# Credits
* Putty source code
* http://stackoverflow.com/questions/128561/registering-a-custom-win32-window-class-from-c-sharp
* David Lechner SshAgentLib (pageant security layer) [https://github.com/dlech/SshAgentLib]


