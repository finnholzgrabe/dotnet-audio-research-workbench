namespace AudioResearch.ML;

/// <summary>
/// A classifier that maps a feature vector to a class label. This is the seam
/// that keeps the model replaceable: the built-in <see cref="KnnClassifier"/>
/// implements it, and an ONNX-backed model could implement the same interface
/// without changing the evaluation code (see docs/onnx.md). Keeping this as a
/// plain interface means no heavy/native runtime dependency is required.
/// </summary>
public interface IFeatureClassifier
{
    /// <summary>Predicts the class label for a single feature vector.</summary>
    string Predict(double[] features);
}
