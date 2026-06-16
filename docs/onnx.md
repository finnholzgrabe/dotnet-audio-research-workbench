# ONNX integration (optional, not bundled)

The project deliberately ships **no ONNX runtime dependency**. The build stays
offline-friendly and deterministic, and CI needs no native packages. What exists
today is the *seam* that makes an ONNX-backed model a drop-in replacement.

## The seam

`AudioResearch.ML.IFeatureClassifier` is the single abstraction the evaluation
code depends on:

```csharp
public interface IFeatureClassifier
{
    string Predict(double[] features);
}
```

`KnnClassifier` implements it. Any other model — including one executed by ONNX
Runtime — can implement the same interface and be evaluated identically.

## How to plug in an ONNX model later

The feature pipeline already exports everything an external trainer needs:

1. **Export features.** Use `features summary` (JSON) or the per-sample feature
   vectors to build a training matrix; the vector schema is stable and named.
2. **Train elsewhere.** Train a model (scikit-learn, PyTorch, etc.) on those
   features in `experiments/python`, then export it to `*.onnx` (e.g. via
   `skl2onnx` or `torch.onnx.export`). Keep the same feature order.
3. **Implement the seam.** Add an optional project (e.g. `AudioResearch.ML.Onnx`)
   that references `Microsoft.ML.OnnxRuntime` and wraps a session:

   ```csharp
   public sealed class OnnxFeatureClassifier : IFeatureClassifier
   {
       // load <model>.onnx in the constructor; run the session in Predict(...)
       public string Predict(double[] features) => /* argmax over ONNX output */;
   }
   ```

4. **Swap it in.** Construct that classifier instead of `KnnClassifier` where the
   baseline is evaluated. No other code changes.

## Why it is optional

- `Microsoft.ML.OnnxRuntime` pulls native binaries and a network restore, which
  conflicts with this repo's offline/deterministic stance.
- Model weights must never be committed (`*.onnx` is already git-ignored).
- Until a concrete model is trained, an ONNX path would be hollow. The seam keeps
  the door open without the cost.
