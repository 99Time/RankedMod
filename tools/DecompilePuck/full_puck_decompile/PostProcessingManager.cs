using Linework.SoftOutline;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessingManager : MonoBehaviourSingleton<PostProcessingManager>
{
	[Header("References")]
	[SerializeField]
	private UniversalRenderPipelineAsset renderPipelineAsset;

	[SerializeField]
	private UniversalRendererData universalRendererData;

	[SerializeField]
	private SoftOutlineSettings puckOutlineSettings;

	private Volume volume;

	public override void Awake()
	{
		base.Awake();
		volume = GetComponent<Volume>();
	}

	public void SetMotionBlur(bool enabled)
	{
		volume.profile.TryGet<MotionBlur>(out var component);
		if ((bool)component)
		{
			component.active = enabled;
		}
	}

	public void SetMsaaSampleCount(int sampleCount)
	{
		renderPipelineAsset.msaaSampleCount = sampleCount;
	}

	public void SetObstructedPuck(bool enabled)
	{
		universalRendererData.rendererFeatures.Find((ScriptableRendererFeature x) => x.name == "Obstructed Puck").SetActive(enabled);
	}

	public void SetPuckOutline(bool enabled)
	{
		puckOutlineSettings.SetActive(enabled);
	}
}
