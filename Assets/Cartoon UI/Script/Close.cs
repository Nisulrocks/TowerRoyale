using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CartoonUI
{
    public class Close : MonoBehaviour
    {
        public new GameObject gameObject;
        public void close()
        {
            gameObject.SetActive(false);
        }
    }
}
