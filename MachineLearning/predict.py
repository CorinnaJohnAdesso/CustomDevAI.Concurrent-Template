import pandas as pd
import pickle

print('Loading AI model');

# open file, deserialize model
with open('model.pkl', 'rb') as handle:
    restored_classifier = pickle.load(handle)

print('Reading skill matrix');

# prepare personal data: remove everything except skills
unknown_rows = pd.read_csv('unknown.csv')
skills = unknown_rows.drop('Name', axis=1)

print('Predicting');

# predict if the person gets a job
p = restored_classifier.predict(skills)

print('Result:\n--------------------');

# display results
result = unknown_rows.assign(prediction=p)
print(result[['Name', 'prediction']])

print('\n--------------------\n')
