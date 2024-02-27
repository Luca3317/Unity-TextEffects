using System.Collections.Generic;
using System.Linq;
using TMPEffects.Components;
using UnityEngine;
using TMPEffects.TMPEvents;

public class TextControllerTest : MonoBehaviour
{
    [SerializeField] TMPAnimator animator;

    List<string> textList = new List<string>()
    {
        "This is just some plaintext\r\n\r\nim <wave>waving</wave>\r\nim <funky>funky</funky>\r\nim <shake>shaking</shake>\r\nim <jump>jumping</jump>\r\nim <pivot axis=(0;1;0)>pivoting</pivot>\r\nim <fade>fading</fade>\r\nim <grow>growing</grow>\r\nim <explode>exploding</explode>\r\nim <char>changing characters</char>\r\nNEW TEXTRTTTwell, that kind of does suck.\r\nA lot of non animated text will slow down the entire thing, ESPECIALLY if it is at the end. Shiet how do i handle that?\r\nSimple Change to interval tree based on the amount of \r\ntext contained in here? idk man\r\nWhatever can i do \r\nMORE MORE MOPRE MORE MOPRE TESXXTT!!!\r\n",
        "This is ANOTHER plaintext\r\n\r\nim <wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave><wave>waving</wave>\r\nim <funky>funky</funky>\r\nim <shake>shaking</shake>\r\nim <jump>jumping</jump>\r\nim <pivot axis=(0;1;0)>pivoting</pivot>\r\nim <fade>fading</fade>\r\nim <grow>growing</grow>\r\nim <explode>exploding</explode>\r\nim <char>changing characters</char>\r\nOkay, well, that kind of does suck.\r\nA lot of non animated text will slow down the entire thing, ESPECIALLY if it is at the end. Shiet how do i handle that?\r\nSimple Change to interval tree based on the amount of \r\ntext contained in here? idk man\r\nWhatever can i do \r\nMORE MORE MOPRE MORE MOPRE TESXXTT!!!</wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave></wave>",
        "Seems <wave>like it doesnt work... i hope the measured values are still kinda accurate",
        "<wave>Hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave><wave>hahaha</wave>",
        "<wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave><wave>Hahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahahaha</wave>"
    };


    [System.NonSerialized] int count = 0;
    [System.NonSerialized] int index = 2;
    bool first = true;


    public void Update()
    {
        //count++;

        //if (count == 1000)
        //{
        //    animator.TextComponent.text = textList[index];
        //    index++;
        //    index %= textList.Count;
        //    count = 0;
        //}
    }


    public void ReceeiveEvent(TMPEventArgs args)
    {
        Debug.Log("EVENT");
        Debug.Log("Received TMPEvent with message " +  args.Tag.Name + " and first parameter " + args.Tag.Parameters.ToList()[0].Key + " " + args.Tag.Parameters.ToList()[0].Value);
    }
}