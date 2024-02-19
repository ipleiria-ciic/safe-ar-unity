// // <summary>
//         /// Initializes and returns an instance of `NonMaxSuppression` object detection layer.
//         /// </summary>
//         /// <param name="name">The name to use for the output tensor of the layer.</param>
//         /// <param name="boxes">The name to use for the boxes tensor of the layer.</param>
//         /// <param name="scores">The name to use for the scores tensor of the layer.</param>
//         /// <param name="maxOutputBoxesPerClass">The name to use for an optional scalar tensor, with the maximum number of boxes to return for each class.</param>
//         /// <param name="iouThreshold">The name to use for optional scalar tensor, with the threshold above which the intersect-over-union rejects a box.</param>
//         /// <param name="scoreThreshold">The name to use for an optional scalar tensor, with the threshold below which the box score filters a box from the output.</param>
//         /// <param name="centerPointBox">The format the `boxes` tensor uses to store the box data as a `CenterPointBox`. The default value is `CenterPointBox.Corners`.</param>
//         public NonMaxSuppression(string name, string boxes, string scores, string maxOutputBoxesPerClass = null, string iouThreshold = null, string scoreThreshold = null, CenterPointBox centerPointBox = CenterPointBox.Corners)
//         {
//             this.name = name;
//             if (scoreThreshold != null)
//                 this.inputs = new[] { boxes, scores, maxOutputBoxesPerClass, iouThreshold, scoreThreshold };
//             else if (iouThreshold != null)
//                 this.inputs = new[] { boxes, scores, maxOutputBoxesPerClass, iouThreshold };
//             else if (maxOutputBoxesPerClass != null)
//                 this.inputs = new[] { boxes, scores, maxOutputBoxesPerClass };
//             else
//                 this.inputs = new[] { boxes, scores };
//             this.centerPointBox = centerPointBox;
//         }

//         /// <inheritdoc/>
//         internal override PartialTensor InferPartialTensor(PartialTensor[] inputTensors, PartialInferenceContext ctx)
//         {
//             var shape = new SymbolicTensorShape(SymbolicTensorDim.Unknown, new SymbolicTensorDim(3));
//             return new PartialTensor(DataType.Int, shape);
//         }

//         /// <inheritdoc/>
//         public override Tensor Execute(Tensor[] inputTensors, ExecutionContext ctx)
//         {
//             var maxOutputBoxesPerClass = inputTensors.Length > 2 && inputTensors[2] != null ? inputTensors[2].ToReadOnlySpan<int>()[0] : 0;
//             var iouThreshold = inputTensors.Length > 3 && inputTensors[3] != null ? inputTensors[3].ToReadOnlySpan<float>()[0] : 0f;
//             var scoreThreshold = inputTensors.Length > 4 && inputTensors[4] != null ? inputTensors[4].ToReadOnlySpan<float>()[0] : 0f;
//             var boxes = inputTensors[0] as TensorFloat;
//             var scores = inputTensors[1] as TensorFloat;

//             // Filter out boxes that have high intersection-over-union (IOU) overlap with previously selected boxes.
//             // Bounding boxes with score less than scoreThreshold are removed.
//             // maxOutputBoxesPerClass represents the maximum number of boxes to be selected per batch per class.
//             // This algorithm is agnostic to where the origin is in the coordinate system and more generally is invariant to orthogonal transformations and translations of the coordinate system; thus translating or reflections of the coordinate system result in the same boxes being selected by the algorithm.
//             // Bounding box format is indicated by attribute centerPointBox. Corners - diagonal y,x pairs (coords or normalized values). Center - center coords + width and height.
//             // iouThreshold represents the threshold for deciding whether boxes overlap too much with respect to IOU. 0-1 range. 0 - no filtering.
//             // The output is a set of integers indexing into the input collection of bounding boxes representing the selected boxes sorted in descending order and grouped by batch and class.

//             ShapeInference.NonMaxSuppression(boxes.shape, scores.shape, iouThreshold);
//             if (boxes.shape.HasZeroDims() || scores.shape.HasZeroDims() || maxOutputBoxesPerClass <= 0)
//                 return ctx.backend.NewOutputTensorInt(new TensorShape(0, 3));

//             ArrayTensorData.Pin(boxes);
//             ArrayTensorData.Pin(scores);

//             // allocate the maximum possible output size tensor
//             var outputData = new int[scores.shape[0] * scores.shape[1] * maxOutputBoxesPerClass * 3];
//             // array of the current selected output indexes
//             var selectedIndexes = new int[maxOutputBoxesPerClass];
//             // array of the current selected output scores
//             var selectedScores = new float[maxOutputBoxesPerClass];
//             // keep a track of total output boxes
//             int numberOfBoxes = 0;

//             // find boxes to keep and then combine them into the single output tensor grouped by current batch and class
//             for (int batch = 0; batch < scores.shape[0]; batch++)
//             {
//                 for (int classID = 0; classID < scores.shape[1]; classID++)
//                 {
//                     //keep a track of selected boxes per batch and class
//                     int selectedBoxes = 0;
//                     Array.Clear(selectedIndexes, 0, maxOutputBoxesPerClass);
//                     Array.Clear(selectedScores, 0, maxOutputBoxesPerClass);
//                     // iterate over input boxes for the current batch and class
//                     for (int i = 0; i < scores.shape[2]; i++)
//                     {
//                         // check if the score is lower that the scoreThreshold
//                         if (scores[batch, classID, i] < scoreThreshold)
//                             continue;

//                         // initialize insert index to last position
//                         int insertIndex = selectedBoxes;
//                         bool isIgnoreBox = false;

//                         // compare input boxes to the already selected boxes
//                         for (int j = 0; j < selectedBoxes; j++)
//                         {
//                             // if insert index is still default, i.e. box score is lower than previous sorted boxes, compare to see if this is the correct insert index
//                             if ((insertIndex == selectedBoxes) && scores[batch, classID, i] > scores[batch, classID, selectedIndexes[j]])
//                                 insertIndex = j;

//                             // if not excessive overlap with this box consider next box
//                             if (NotIntersectOverUnion(
//                                     boxes[batch, i, 0],
//                                     boxes[batch, i, 1],
//                                     boxes[batch, i, 2],
//                                     boxes[batch, i, 3],
//                                     boxes[batch, selectedIndexes[j], 0],
//                                     boxes[batch, selectedIndexes[j], 1],
//                                     boxes[batch, selectedIndexes[j], 2],
//                                     boxes[batch, selectedIndexes[j], 3],
//                                     centerPointBox, iouThreshold))
//                                 continue;

//                             // new box has lower score than overlap box so do not output new box
//                             if (insertIndex >= selectedBoxes)
//                             {
//                                 isIgnoreBox = true;
//                                 break;
//                             }

//                             // new box has higher score than overlap box so remove overlap box from list, no need to shift memory if it is in final position
//                             if (j < (maxOutputBoxesPerClass - 1))
//                             {
//                                 // remove the overlaping box index and score values from the current selected box array by shifting the memory
//                                 // selectedIndexes/selectedScores = [x x x j y y y]
//                                 // <- shift y y y by one
//                                 // [x x x y y y]
//                                 unsafe
//                                 {
//                                     fixed (int* dst = &selectedIndexes[j])
//                                         UnsafeUtility.MemMove(dst, dst + 1, (maxOutputBoxesPerClass - (j + 1)) * sizeof(int));
//                                     fixed (float* dst = &selectedScores[j])
//                                         UnsafeUtility.MemMove(dst, dst + 1, (maxOutputBoxesPerClass - (j + 1)) * sizeof(int));
//                                 }
//                             }
//                             selectedBoxes--;
//                             j--;
//                         }

//                         // either new box has lower score than an overlap box or there are already maxOutputBoxesPerClass with a better score, do not output new box
//                         if (isIgnoreBox || insertIndex >= maxOutputBoxesPerClass)
//                             continue;

//                         // shift subsequent boxes forward by one in sorted array to make space for new box, no need if new box is after all boxes or or at end of array
//                         if (insertIndex < selectedBoxes && insertIndex < (maxOutputBoxesPerClass - 1))
//                         {
//                             // shift memory to free a slot for a new box index and score values
//                             // selectedIndexes/selectedScores = [x x x y y y]
//                             // -> shift y y y by one
//                             // [x x x insertIndex y y y]
//                             unsafe
//                             {
//                                 fixed (int* dst = &selectedIndexes[insertIndex])
//                                     UnsafeUtility.MemMove(dst + 1, dst, (maxOutputBoxesPerClass - (insertIndex + 1)) * sizeof(int));
//                                 fixed (float* dst = &selectedScores[insertIndex])
//                                     UnsafeUtility.MemMove(dst + 1, dst, (maxOutputBoxesPerClass - (insertIndex + 1)) * sizeof(int));
//                             }
//                         }

//                         // record the score and index values of the selected box
//                         // [x x x insertIndex y y y]
//                         // insert box
//                         // [x x x i y y y]
//                         // [x x x score y y y]
//                         selectedIndexes[insertIndex] = i;
//                         selectedScores[insertIndex] = scores[batch, classID, i];
//                         selectedBoxes = Mathf.Min(maxOutputBoxesPerClass, selectedBoxes + 1);
//                     }

//                     // gather outputs
//                     for (int i = 0; i < selectedBoxes; i++)
//                     {
//                         // box is identified by its batch, class and index
//                         outputData[numberOfBoxes * 3 + 0] = batch;
//                         outputData[numberOfBoxes * 3 + 1] = classID;
//                         outputData[numberOfBoxes * 3 + 2] = selectedIndexes[i];
//                         numberOfBoxes++;
//                     }
//                 }
//             }

//             // create output tensor of correct length by trimming outputData
//             var O = ctx.backend.NewOutputTensorInt(new TensorShape(numberOfBoxes, 3));
//             NativeTensorArray.Copy(outputData, ArrayTensorData.Pin(O).array, numberOfBoxes * 3);
//             return O;
//         }