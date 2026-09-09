using UnityEditor;
using UnityEngine;

namespace rayzngames
{	
	#region CustomInspector
	[CustomEditor(typeof(BicycleVehicle))]
		//We need to extend the Editor
		public class BicycleInspector : Editor
		{
			//Here we grab a reference to our component
			BicycleVehicle bicycle;

			private void OnEnable()
			{
				//target is by default available for you in Editor		
				bicycle = target as BicycleVehicle;
			}

			//Here is the meat of the script
			public override void OnInspectorGUI()
			{
				SetLabel("Easy Bike System", 30, FontStyle.Bold, TextAnchor.UpperLeft);
				base.OnInspectorGUI();			
				SetLabel("", 12, FontStyle.Italic, TextAnchor.LowerRight);
				SetLabel("Love from RayznGames", 12, FontStyle.Italic, TextAnchor.LowerRight);
			}
			void SetLabel(string title, int size, FontStyle style, TextAnchor alignment)
			{			
				GUI.skin.label.alignment = alignment;
				GUI.skin.label.fontSize = size;
				GUI.skin.label.fontStyle = FontStyle.Bold;
				GUILayout.Label(title);
			}		
		}	

		#endregion

}
