"""Mission 15 - Researcher 1 training pipeline.

Loads the student performance dataset, preprocesses it, evaluates a
regression model with RMSE, retrains on the full data, and saves the
final pipeline to <output-dir>/model.pkl.

Distilled from modeling.ipynb so the whole process runs unattended
inside a Docker container.
"""
import argparse
import logging
import os
import sys

import joblib
import numpy as np
import pandas as pd
from sklearn.linear_model import LinearRegression
from sklearn.metrics import mean_squared_error, r2_score
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

TARGET_COLUMN = "Performance Index"
CATEGORICAL_COLUMN = "Extracurricular Activities"
MODEL_FILENAME = "model.pkl"

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
)
logger = logging.getLogger(__name__)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train student performance regressor")
    parser.add_argument(
        "--train-csv",
        default="data/mission15_train.csv",
        help="Path to the training CSV file",
    )
    parser.add_argument(
        "--output-dir",
        default="output",
        help="Directory where model.pkl is written",
    )
    parser.add_argument("--test-size", type=float, default=0.2)
    parser.add_argument("--seed", type=int, default=42)
    return parser.parse_args()


def load_dataset(csv_path: str) -> pd.DataFrame:
    df = pd.read_csv(csv_path)
    missing = [c for c in (TARGET_COLUMN, CATEGORICAL_COLUMN) if c not in df.columns]
    if missing:
        raise ValueError(f"Required columns not found: {missing}")
    return df


def preprocess(df: pd.DataFrame) -> pd.DataFrame:
    """Encode Yes/No to 1/0 and drop duplicated rows."""
    out = df.drop_duplicates().copy()
    out[CATEGORICAL_COLUMN] = out[CATEGORICAL_COLUMN].map({"Yes": 1, "No": 0})
    if out[CATEGORICAL_COLUMN].isna().any():
        raise ValueError(f"Unexpected value in '{CATEGORICAL_COLUMN}'")
    out[CATEGORICAL_COLUMN] = out[CATEGORICAL_COLUMN].astype("int64")
    return out


def build_pipeline() -> Pipeline:
    """Standardize features then fit a linear regressor."""
    return Pipeline([
        ("scaler", StandardScaler()),
        ("regressor", LinearRegression()),
    ])


def main() -> int:
    args = parse_args()

    try:
        train_df = load_dataset(args.train_csv)
    except (FileNotFoundError, ValueError, pd.errors.ParserError) as error:
        logger.error("Failed to load training data: %s", error)
        return 1

    logger.info("Loaded %s rows from %s", len(train_df), args.train_csv)

    try:
        clean_df = preprocess(train_df)
    except ValueError as error:
        logger.error("Preprocessing failed: %s", error)
        return 1

    logger.info("Rows after dropping duplicates: %s", len(clean_df))

    feature_columns = [c for c in clean_df.columns if c != TARGET_COLUMN]
    X = clean_df[feature_columns]
    y = clean_df[TARGET_COLUMN]

    # Hold-out evaluation to report RMSE before the final fit
    X_train, X_valid, y_train, y_valid = train_test_split(
        X, y, test_size=args.test_size, random_state=args.seed,
    )
    eval_pipeline = build_pipeline()
    eval_pipeline.fit(X_train, y_train)
    pred = eval_pipeline.predict(X_valid)
    rmse = float(np.sqrt(mean_squared_error(y_valid, pred)))
    r2 = float(r2_score(y_valid, pred))
    logger.info("Validation RMSE: %.4f, R2: %.4f", rmse, r2)

    # Retrain on the full dataset for the final artifact
    final_pipeline = build_pipeline()
    final_pipeline.fit(X, y)

    try:
        os.makedirs(args.output_dir, exist_ok=True)
        model_path = os.path.join(args.output_dir, MODEL_FILENAME)
        joblib.dump(final_pipeline, model_path)
    except OSError as error:
        logger.error("Failed to save model: %s", error)
        return 1

    logger.info("Model saved: %s (%d bytes)", model_path, os.path.getsize(model_path))
    return 0


if __name__ == "__main__":
    sys.exit(main())
