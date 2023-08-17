Avid5
=====

Avid5 is the fifth iteration of my "Avid" (Audio Visual Integrated Delivery) software, which is my personal 
system that I use for watching, recording and playing TV, movies, music and photos, etc.

It integrates:

- Digital broadcast TV and radio
- TV time-shifting
- On-disk TV & radio recording and playback
- Integrated electronic program guide for scheduled recordings
- Watching recorded TV or other video files
- Audio jukebox for my music collection
- Spotify streamed music
- Displaying photo albums
- Streaming services on a Roku box
- Streaming services via Chromecast
- Home security (switching lights and radio randomly)

In so far as anything is novel in this line, the novelty is in the way in which it is controlled.
The significant point is that a single touch phone or tablet user interface controls everything, 
in a totally integrated fashion. 
The remote control is fully bidirectional, informative and interactive. It is just a web application for a phone or tablet.
There are no conventional infrared controlers anywhere.
There is no conventional desktop interaction.
Most player functions look and behave the same way, irrespective of what source is being played.
It was very important that it all could be used by non-technical people. 
It also has to look right, and fit in to a living room environment.

This is the fifth such system I have built since 2003. The first one (obviously known as "Avid") 
used a Pocket PC for remote control but was retired as a result of hardware unreliability after over 
six years continuous service. The second one ("Avid2") was retired after only a year as a result of 
major refurbishment of the living room and the AV equipment. Many of the original "Avid" ideas  
survived intact in Avid4, though with a "touch" flavour in place of a stylus. 
Also the capability grew over the years, with the addition of (e.g.) streaming, Spotify and security.
Earlier versions supported a Sky satellite box, smart TV and some web-based streaming services. 
These capabilities have now been retired.

Avid5 is a re-write (after about 10 years use of Avid4) to upgrade to a more modern software Platform (.Net 6),
to optionally move to a Linux operating system (in addition to the original Windows 10/11),
to tidy up the code, removing retired mechanisms.

Original Avid was obviously much more novel in its time. But I remain surprised that there are 
still few commercial offerings with a fully bi-directional remote control. 
Once used to the flexibility offered by such systems, it is extremely difficult to return to "old fashioned" 
one-way InfraRed controllers.

The key hardware and software components are:
- a PC, running Windows 10/11 or Linux KDE
- a Yamaha AV receiver (Yamaha is required)
- a TV screen, which is used solely as an HDMI display
- a TV tuner (or tuners) - either BDA PCI cards (Windows only) or an HDHomeRun streaming tuner somewhere on the local network
- a Pulse-8 CEC USB controller to turn the TV screen on and off
- a Roku box (for streaming)
- J River Media Center player software
- a Spotify account (for Spotify)
- this software!

All the software and documentation I have developed for Avid5 is in this repository and freely available 
under the MIT license.

Note: The name for my project was chosen before I discovered the existence of the Avid digital video editing systems. 
I assume no-one will be confused by this. I'm certainly not!

