using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LineOfSight : MonoBehaviour 
{
	public float range = 20f;
	public float searchrange = 20f;
	private float offset = .1f;


	public GameObject lightsource;
	private PolygonCollider2D[] allCollidersInScene;
	// Use this for initialization
	void Start () 
	{
		allCollidersInScene = FindObjectsOfType<PolygonCollider2D>();
		Mesh _mesh = new Mesh();
		GetComponent<MeshFilter>().mesh = _mesh;
	}
	
	// Update is called once per frame
	void Update () 
	{
		generateLOSMesh();
	}

	private void generateLOSMesh()
	{
		if(lightsource == null || allCollidersInScene == null)
		{
			return;
		}
		int layermask = 1 << 9; //layer 9 is the ignoreLoS layer
		layermask = ~layermask;
		List<Vector3> verts = new List<Vector3>(); //Use Vector3 to store miscelaneous information in the z coordinate
		Vector3 lightpos = lightsource.transform.position;
		//verts.Add(new Vector2(0, 0)); //vertices are in local space
		for(int j = 0; j < allCollidersInScene.Length; j++)
		{
			PolygonCollider2D other = allCollidersInScene[j];
			Vector3 dir = other.transform.position - lightpos;
			if(dir.magnitude <= searchrange) //the collider is in range //CAN MISS LARGE SCALED OBJECTS
			{
				Debug.DrawLine(lightpos, other.transform.position, Color.blue);
				for(int k = 0; k < ((PolygonCollider2D)other).points.Length; k++) //Try all of the corners of the PolygonCollider2D
				{
					Vector2 pointworldspace = ((PolygonCollider2D)other).points[k];
					pointworldspace.Scale(new Vector2(other.transform.localScale.x, other.transform.localScale.y));
					pointworldspace += new Vector2(other.transform.position.x , other.transform.position.y);
					dir = (Vector3)pointworldspace - lightpos;
					RaycastHit2D hit = Physics2D.Raycast(lightpos, dir, range, layermask);
					if(hit.collider == other) //Corner within range
					{
						Vector3 point = hit.point;
						Debug.DrawLine(lightpos, hit.point, Color.green);
						Vector2 pointtoadd1 = point - lightpos + dir.normalized * offset;
						hit = Physics2D.Raycast(point + dir.normalized * offset, dir, range - (point - lightpos).magnitude -  offset, layermask);
						if(hit.collider == null) //that corner is the only thing
						{
							Debug.DrawLine(point + dir.normalized * offset, lightpos + dir.normalized * range, Color.red);
							verts.Add (new Vector3(pointtoadd1.x, pointtoadd1.y, j + 0.1f)); //Store the color and which polycollider in the z value
							Vector2 pointtoadd2 = dir.normalized * range;
							verts.Add (new Vector3(pointtoadd2.x, pointtoadd2.y, j + 0.1f)); //both red
						}
						else if(hit.collider == other) //internal object corner
						{
							Debug.DrawLine(point + dir.normalized * offset, lightpos + dir.normalized * range, Color.grey);
							verts.Add (new Vector3(pointtoadd1.x, pointtoadd1.y, j)); //whole number for grey
							Vector2 pointtoadd2 = dir.normalized * range;
							verts.Add (new Vector3(pointtoadd2.x, pointtoadd2.y, j)); //both grey
						}
						else //there is another object behind this corner
						{
							Debug.DrawLine(point + dir.normalized * offset, (Vector3)hit.point + dir.normalized * offset, Color.red);
							verts.Add (new Vector3(pointtoadd1.x, pointtoadd1.y, j + 0.1f)); //first is red
							Debug.DrawLine((Vector3)hit.point + dir.normalized * offset, lightpos + dir.normalized * range, Color.gray);
							Vector2 pointtoadd2 = (Vector2)hit.point + (Vector2)dir.normalized * offset - (Vector2)lightpos;
							verts.Add(new Vector3(pointtoadd2.x, pointtoadd2.y, j));
							Vector2 pointtoadd3 = dir.normalized * range;
							verts.Add (new Vector3(pointtoadd3.x, pointtoadd3.y, j)); //both grey

						}
					}
					else if(hit.collider != null) //found a different object first
					{
						if(Vector2.Distance(lightpos, hit.point) > Vector2.Distance(lightpos, pointworldspace)) //is this because we missed the corner? if so...
						{
							Debug.DrawLine((Vector3)pointworldspace + dir.normalized * offset, (Vector3)hit.point + dir.normalized * offset, Color.red);
							Vector2 pointtoadd1 = pointworldspace - (Vector2)lightpos + (Vector2)dir.normalized * offset;
							verts.Add (new Vector3(pointtoadd1.x, pointtoadd1.y, j + 0.1f));
							Debug.DrawLine((Vector3)hit.point + dir.normalized * offset, lightpos + dir.normalized * range, Color.gray);
							Vector2 pointtoadd2 = hit.point + (Vector2)dir.normalized * offset - (Vector2)lightpos;
							Vector2 pointtoadd3 = dir.normalized * range;
							verts.Add (new Vector3(pointtoadd2.x, pointtoadd2.y, j));
							verts.Add (new Vector3(pointtoadd3.x, pointtoadd3.y, j));
						}
					}
					else //missed the corner for some reason or another, still need to consider it
					{
						Debug.DrawLine(lightpos, pointworldspace, Color.green);
						Debug.DrawLine((Vector3)pointworldspace + dir.normalized * offset, lightpos + dir.normalized * range, Color.red);
						Vector2 pointtoadd1 = pointworldspace - (Vector2)lightpos + (Vector2)dir.normalized * offset;
						Vector2 pointtoadd2 = dir.normalized * range;
						verts.Add(new Vector3(pointtoadd1.x, pointtoadd1.y, j + 0.1f));
						verts.Add(new Vector3(pointtoadd2.x, pointtoadd2.y, j + 0.1f));
						//add the corner anyways
					}
				}
			}
		}


		//Sort vertices by angle (pseudoangle?)
		PseudoangleComparator pscomp = new PseudoangleComparator();
		verts.Sort (pscomp);
		Vector3[] vertices = verts.ToArray();
		/*for(int a = 0; a < vertices.Length; a++)
		{
			Debug.Log (vertices[a].x + ", " + vertices[a].y + ", " + vertices[a].z + " : " + pscomp.pseudoangle(vertices[a].x, vertices[a].y) + " dist: " + pscomp.distance(vertices[a].x, vertices[a].y));
		}*/

		//triangulate
		List<int> tris = new List<int>();
		int x = 0;
		while(x < vertices.Length)
		{
			int r1nv = 2;
			int r1v1 = x;
			int r1v2 = x+1;
			if(vertices[r1v1].z%1.0 == 0.0) //grey, only 2 points
			{
				int r2v1 = (x+2)%vertices.Length;
				int r2v2 = (x+3)%vertices.Length;
				if(vertices[r2v1].z%1.0 == 0.0) //both only grey, box it
				{
					tris.Add (r1v1);
					tris.Add (r1v2);
					tris.Add (r2v2);
					//
					tris.Add (r2v2);
					tris.Add (r2v1);
					tris.Add (r1v1);
				}
				else if(vertices[r2v2].z%1.0 == 0.0) //grey/tri
				{
					int r2v3 = (x+4)%vertices.Length;
					//box the grey
					tris.Add (r1v1);
					tris.Add (r1v2);
					tris.Add (r2v2);
					//
					tris.Add (r2v2);
					tris.Add (r1v2);
					tris.Add (r2v3);

					if((int)vertices[r1v1].z == (int)vertices[r2v1].z) //same collider, connect them
					{
						tris.Add (r2v1);
						tris.Add (r1v1);
						tris.Add (r2v2);
					}

				}
				else //grey/red box if same collider origin
				{
					if((int)vertices[r1v1].z == (int)vertices[r2v1].z)
					{
						tris.Add (r1v1);
						tris.Add (r1v2);
						tris.Add (r2v2);
						//
						tris.Add (r2v2);
						tris.Add (r2v1);
						tris.Add (r1v1);
					}
				}
			}
			else if(vertices[r1v2].z%1.0 == 0.0) //first is red, second (and therefore third) are grey
			{
				int r1v3 = x+2;
				r1nv = 3;
				int r2v1 = (x+3)%vertices.Length;
				int r2v2 = (x+4)%vertices.Length;
				if(vertices[r2v1].z%1.0 == 0.0) //tri/grey, box it
				{
					tris.Add (r1v2);
					tris.Add (r1v3);
					tris.Add (r2v2);

					tris.Add (r2v1);
					tris.Add (r1v2);
					tris.Add (r2v2);

					if((int)vertices[r1v1].z == (int)vertices[r2v1].z)
					{
						tris.Add (r1v1);
						tris.Add (r1v2);
						tris.Add (r2v1);
					}
				}
				else if(vertices[r2v2].z%1.0 == 0.0)//tri/tri
				{
					int r2v3 = (x+5)%vertices.Length;

					tris.Add (r1v2);
					tris.Add (r1v3);
					tris.Add (r2v3);

					tris.Add (r2v3);
					tris.Add (r2v2);
					tris.Add (r1v2);
					//thats it?
				}
				else//tri/red
				{
					tris.Add (r1v2);
					tris.Add (r1v3);
					tris.Add (r2v2);
					
					tris.Add (r2v1);
					tris.Add (r1v2);
					tris.Add (r2v2);

					if((int)vertices[r1v1].z == (int)vertices[r2v1].z)
					{
						tris.Add (r1v1);
						tris.Add (r1v2);
						tris.Add (r2v1);
					}
				}
			}
			else //2 red
			{
				int r2v1 = (x+2)%vertices.Length;
				int r2v2 = (x+3)%vertices.Length;
				if(vertices[r2v1].z%1.0 == 0.0) //red/grey box if same collider origin
				{
					if((int)vertices[r1v1].z == (int)vertices[r2v1].z)
					{
						tris.Add (r1v1);
						tris.Add (r1v2);
						tris.Add (r2v2);
						//
						tris.Add (r2v2);
						tris.Add (r2v1);
						tris.Add (r1v1);
					}
				}
				else if(vertices[r2v2].z%1.0 == 0.0) //red/tri
				{
					int r2v3 = (x+4)%vertices.Length;

					tris.Add (r1v1);
					tris.Add (r1v2);
					tris.Add (r2v2);
					//
					tris.Add (r2v2);
					tris.Add (r1v2);
					tris.Add (r2v3);
					
					if((int)vertices[r1v1].z == (int)vertices[r2v1].z) //same collider, connect them
					{
						tris.Add (r2v1);
						tris.Add (r1v1);
						tris.Add (r2v2);
					}
				}
				else //red/red box if same
				{
					if((int)vertices[r1v1].z == (int)vertices[r2v1].z)
					{
						tris.Add (r1v1);
						tris.Add (r1v2);
						tris.Add (r2v2);
						//
						tris.Add (r2v2);
						tris.Add (r2v1);
						tris.Add (r1v1);
					}
				}
			}
			x+=r1nv;
		}




		int[] triangles = tris.ToArray();
		for(int v = 0; v < vertices.Length; v++)
		{
			vertices[v].z = -1;
		}







		Mesh mesh = GetComponent<MeshFilter>().mesh;
		mesh.Clear();
		mesh.vertices = vertices;
		mesh.triangles = triangles;

	}


	private class PseudoangleComparator : IComparer<Vector3>
	{
		public int Compare(Vector3 arg1, Vector3 arg2)
		{
			float pseudoangle1 = pseudoangle(arg1.x, arg1.y);
			float pseudoangle2 = pseudoangle(arg2.x, arg2.y);
			if(IsApproximately(pseudoangle1, pseudoangle2, .000001f))
			//if(pseudoangle1 == pseudoangle2)
			{
				if(distance (arg1.x, arg1.y) > (distance(arg2.x, arg2.y)))
				{
					return 1;
				}
				else if(distance (arg1.x, arg1.y) < (distance(arg2.x, arg2.y)))
				{
					return -1;
				}
			}
			else if(pseudoangle1 > pseudoangle2)
			{
				return -1;
			}
			return 1;
		}
		public int Compareafgh(Vector3 arg1, Vector3 arg2)
		{
			float pseudoangle1 = pseudoangle(arg1.x, arg1.y);
			float pseudoangle2 = pseudoangle(arg2.x, arg2.y);
			//Debug.Log (pseudoangle1 + ", " + pseudoangle2);
			if(pseudoangle1 > pseudoangle2)
			{
				return -1;
			}
			else if(pseudoangle1 < pseudoangle2)
			{
				return 1;
			}
			else
			{
				if(distance (arg1.x, arg1.y) > (distance(arg2.x, arg2.y)))
				{
					return 1;
				}
				else if(distance (arg1.x, arg1.y) < (distance(arg2.x, arg2.y)))
				{
					return -1;
				}
			}
			return 0;
		}
		public float pseudoangle(float dx, float dy)
		{
			float p = dx/(Mathf.Abs(dx)+Mathf.Abs(dy)); // -1 .. 1 increasing with x
			if(dy < 0)
			{
				return p - 1;  // -2 .. 0 increasing with x
			}
			else
			{
				return 1 - p;  //  0 .. 2 decreasing with x
			}
			
		}
		private bool IsApproximately(float arg1, float arg2, float tolerance)
		{
			return (Mathf.Abs (arg1 - arg2) <= tolerance);
		}
		public float distance(float x, float y)
		{
			return Mathf.Sqrt((x*x)+(y*y));
		}
	}
}
