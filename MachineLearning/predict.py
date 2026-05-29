import pandas as pd
import pickle

# TODO: open file, deserialize model

# prepare personal data: remove everything except skills
unknown_rows = pd.read_csv('unknown.csv')
skills = unknown_rows.drop('name', axis=1)

# TODO: predict if the person gets a job

# display results
result = unknown_rows.assign(prediction=p)
print(result[['name', 'prediction']])
