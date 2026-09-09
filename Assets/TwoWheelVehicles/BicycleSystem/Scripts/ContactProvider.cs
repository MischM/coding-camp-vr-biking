using UnityEngine;

namespace rayzngames 
{
	public class ContactProvider : MonoBehaviour
	{
		bool contact;  
		public bool GetContact() 
		{
			return contact;
		}
		private void OnTriggerStay(Collider other)
		{
			contact = true;
		}
		private void OnTriggerExit(Collider other)
		{
			contact = false;
		}
	}
}