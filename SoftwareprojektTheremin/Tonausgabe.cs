using System;
using NAudio.Utils;
using NAudio.Wave;
using System;
using System.Windows.Forms;
using NAudio.Wave.SampleProviders;
using NAudio.Wave.WaveFormats;
using System.Windows.Forms;
using System.Diagnostics;
using NAudio;


namespace NAudio
{
    public class Tonausgabe
    {
       

        public static void gen(int freq, float volume)
        {
            // keine Tonausgabe, falls Freuqenz kleiner als 37
            if (freq < 37) { }
            else
            {
                WaveOut _waveOutGene = new WaveOut();
                SignalGenerator /*WaveGenerator*/ wg = new SignalGenerator();
                wg.Type = SignalGeneratorType.Sin;
                wg.Frequency = freq;
                _waveOutGene.Volume = volume;

                _waveOutGene.Init(wg);

                //solange Ton ausgeben bis Thread beendet wird
                while (true)
                {
                    _waveOutGene.Play();
                }
                _waveOutGene.Dispose();
            }
        }

    }
}




/*  Methode als Thread aufrufen
 *  
 *  zu Beginn: (keine Tonausgabe)
 *          int frequenz = 37;
            float volume = 0.5F;
            
            Thread thread = new Thread(delegate () { Tonausgabe.gen(frequenz,volume); });
              thread.Start();

    während des Spielens:
    frequenz = y-Koordinate linke Hand
    volume = y-Koordinate rechte Hand/100  -> Wert für Lautstärke muss zwischen 0 und 1 liegen
    thread.Abort();
    thread = new Thread(delegate () { Tonausgabe.gen(frequenz, volume); });
    thread.Start();
    */

/* Test ohne Kamera
 * 
        int frequenz = 37;
        float volume = 0.5F;


        Thread thread = new Thread(delegate () { Class1.gen(frequenz,volume); });
          thread.Start();

        Stopwatch uhr = new Stopwatch();
        uhr.Start();

        while (uhr.Elapsed.TotalSeconds < 5)
        {

        }
        thread.Abort();

        thread = new Thread(delegate () { Class1.gen(262, 1); });
        thread.Start();

        uhr = new Stopwatch();
        uhr.Start();

        while (uhr.Elapsed.TotalSeconds < 5)
        {

        }
        thread.Abort();

        thread = new Thread(delegate () { Class1.gen(349, volume); });
        thread.Start();

        uhr = new Stopwatch();
        uhr.Start();

        while (uhr.Elapsed.TotalSeconds < 5)
        {

        }
        thread.Abort();

    }
    */

/* Probleme:
 *Meldung: "WaveOut device was not closed", da _waveOutGene.Dispose(); nie aufgerufen wird
 * /
